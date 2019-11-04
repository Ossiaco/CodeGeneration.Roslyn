namespace CodeGeneration.Chorus
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    [DebuggerDisplay("{TypeSymbol?.Name}")]
    internal class MetaType
    {
        private ImmutableHashSet<MetaProperty> _localProperties;
        private ImmutableHashSet<MetaProperty> _allProperties;
        private ImmutableHashSet<MetaProperty> _inheritedProperties;
        private ImmutableArray<MetaType>? _ancestors;
        private ImmutableArray<MetaType>? _allInterfaces;
        private ImmutableArray<MetaType>? _explicitInterfaces;
        private MetaType? _directAncestor;

        public MetaType(INamedTypeSymbol typeSymbol, BaseTypeDeclarationSyntax declarationSyntax, SemanticModel semanticModel)
        {
            _localProperties = null;
            _allProperties = null;
            _inheritedProperties = null;
            _ancestors = null;
            _allInterfaces = null;
            _explicitInterfaces = null;
            _directAncestor = null;

            TypeSymbol = typeSymbol;
            DeclarationSyntax = declarationSyntax;
            SemanticModel = semanticModel;
            //ISerializeable = IsAssignableFrom(CodeGen.IJsonSerializeableType);

            var codeGenAttribute = typeSymbol?.GetAttributes().FirstOrDefault(a => a.AttributeClass.IsOrDerivesFrom<GenerateClassAttribute>());
            if (codeGenAttribute != null)
            {
                var attribute = codeGenAttribute.NamedArguments.FirstOrDefault(v => v.Key == nameof(GenerateClassAttribute.AbstractAttributeType)).Value;
                var field = codeGenAttribute.NamedArguments.FirstOrDefault(v => v.Key == nameof(GenerateClassAttribute.AbstractField)).Value;
                AbstractAttribute = (INamedTypeSymbol)attribute.Value;
                AbstractJsonProperty = (string)field.Value;
                IsAbstract = (AbstractAttribute != null && AbstractJsonProperty != null);
            }
            else
            {
                AbstractJsonProperty = null;
                AbstractAttribute = null;
                IsAbstract = false;
            }

            var stringEnumAttribute = typeSymbol?.GetAttributes().FirstOrDefault(a => a.AttributeClass.IsOrDerivesFrom<JsonStringEnumAttribute>());
            if (stringEnumAttribute != null)
            {
                IsEnumAsString = true;
                JsonStringEnumFormat = (JsonStringEnumFormat)stringEnumAttribute.ConstructorArguments[0].Value;
            }
        }

        private async Task<MetaType> SafeGetTypeAsync(INamedTypeSymbol symbol)
        {
            async Task<MetaType> GetMetaTypeForSymbolAsync(INamedTypeSymbol typeSymbol)
            {
                if (typeSymbol.DeclaringSyntaxReferences.Length > 0)
                {
                    var syntax = await typeSymbol.DeclaringSyntaxReferences[0].GetSyntaxAsync();
                    var inputSemanticModel = SemanticModel.Compilation.GetSemanticModel(syntax.SyntaxTree);
                    return new MetaType(typeSymbol, syntax as BaseTypeDeclarationSyntax, inputSemanticModel);
                }
                return new MetaType(typeSymbol, null, SemanticModel);

            };
            if (symbol == null)
            {
                return new MetaType(symbol, null, SemanticModel);
            }
            return CodeGen.AllNamedTypeSymbols.TryGetValue(symbol, out var result) ? result : await GetMetaTypeForSymbolAsync(symbol);
        }

        //public bool ISerializeable { get; }
        public bool IsEnumAsString { get; }
        public JsonStringEnumFormat JsonStringEnumFormat { get; }
        public SemanticModel SemanticModel { get; }
        public BaseTypeDeclarationSyntax DeclarationSyntax { get; }
        public INamedTypeSymbol TypeSymbol { get; private set; }
        public INamedTypeSymbol AbstractAttribute { get; }
        public bool IsAbstract { get; }
        public string AbstractJsonProperty { get; }
        public IdentifierNameSyntax ClassName => (DeclarationSyntax as InterfaceDeclarationSyntax)?.ClassName() ?? SyntaxFactory.IdentifierName(DeclarationSyntax.Identifier);
        public SyntaxToken ClassNameIdentifier => ClassName.Identifier;
        public SyntaxToken InterfaceNameIdentifier => DeclarationSyntax.Identifier;

        public NameSyntax TypeSyntax
        {
            get { return TypeSymbol.GetFullyQualifiedSymbolName(); }
        }

        public INamespaceSymbol ContainingNamespace
        {
            get { return TypeSymbol.ContainingNamespace; }
        }

        public bool IsDefault
        {
            get { return TypeSymbol == null; }
        }

        public async Task<ImmutableHashSet<MetaProperty>> GetLocalPropertiesAsync()
        {
            if (_localProperties != null)
            {
                return _localProperties;
            }
            if (IsDefault)
            {
                return _localProperties = ImmutableHashSet<MetaProperty>.Empty;
            }

            var derived = (await GetInheritedPropertiesAsync()).ToImmutableHashSet(MetaProperty.DefaultNameTypeComparer);
            var result = new HashSet<MetaProperty>(MetaProperty.DefaultNameTypeComparer);
            foreach (var i in await GetExplicitInterfacesAsync())
            {
                foreach (var prop in await i.GetLocalPropertiesAsync())
                {
                    if (!derived.Contains(prop) && !result.Contains(prop))
                    {
                        result.Add(prop);
                    }
                }
            }

            var that = this;
            var query = TypeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(f => !f.IsPropertyIgnored())
                .Select(p => new MetaProperty(that, p));

            foreach (var prop in query)
            {
                if (!derived.Contains(prop) && !result.Contains(prop))
                {
                    result.Add(prop);
                }
            }
            return _localProperties = result.ToImmutableHashSet();
        }

        public async Task<ImmutableHashSet<MetaProperty>> GetAllPropertiesAsync()
        {
            if (_allProperties != null)
            {
                return _allProperties;
            }
            if (IsDefault)
            {
                return _allProperties = ImmutableHashSet<MetaProperty>.Empty;
            }

            var directAncestor = await GetDirectAncestorAsync();
            var result = directAncestor.IsDefault
                ? new HashSet<MetaProperty>(MetaProperty.DefaultComparer)
                : new HashSet<MetaProperty>(await directAncestor.GetAllPropertiesAsync(), MetaProperty.DefaultComparer);

            foreach (var p in await GetLocalPropertiesAsync())
            {
                if (!result.Contains(p))
                {
                    result.Add(p);
                }
            }
            return _allProperties = result.ToImmutableHashSet();
        }

        public async Task<ImmutableHashSet<MetaProperty>> GetInheritedPropertiesAsync()
        {
            if (_inheritedProperties != null)
            {
                return _inheritedProperties;
            }
            if (IsDefault)
            {
                return _inheritedProperties = ImmutableHashSet<MetaProperty>.Empty;
            }

            var directAncestor = await GetDirectAncestorAsync();
            return _inheritedProperties = (await directAncestor.GetAllPropertiesAsync());
        }

        private class EmptyMetaTypeGeneration : IGrouping<int, MetaType>
        {
            internal static readonly IGrouping<int, MetaType> Default = new EmptyMetaTypeGeneration();

            private EmptyMetaTypeGeneration() { }

            public int Key { get; }

            public IEnumerator<MetaType> GetEnumerator() => Enumerable.Empty<MetaType>().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private class EmptyMetaPropertyGeneration : IGrouping<int, MetaProperty>
        {
            internal static readonly IGrouping<int, MetaProperty> Default = new EmptyMetaPropertyGeneration();

            private EmptyMetaPropertyGeneration() { }

            public int Key { get; }

            public IEnumerator<MetaProperty> GetEnumerator() => Enumerable.Empty<MetaProperty>().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public async Task<MetaType> GetDirectAncestorAsync()
        {
            if (_directAncestor != null)
            {
                return _directAncestor;
            }
            if (DeclarationSyntax?.BaseList is BaseListSyntax baselist && baselist.Types[0].Type is IdentifierNameSyntax nameSyntax)
            {
                var symbol = (INamedTypeSymbol)SemanticModel.GetTypeInfo(nameSyntax).Type;
                if (!symbol.Equals(CodeGen.IJsonSerializeableType))
                {
                    return _directAncestor = await SafeGetTypeAsync(symbol);
                }
            }
            return _directAncestor = new MetaType(null, null, SemanticModel);
        }

        public async Task<IEnumerable<MetaType>> GetAllInterfacesAsync()
        {
            if (_allInterfaces != null)
            {
                return _allInterfaces;
            }
            if (IsDefault)
            {
                return _allInterfaces = ImmutableArray<MetaType>.Empty;
            }
            var that = this;
            return _allInterfaces = (await Task.WhenAll(TypeSymbol.AllInterfaces.Select(SafeGetTypeAsync))).ToImmutableArray();
        }

        public async Task<IEnumerable<MetaType>> GetExplicitInterfacesAsync()
        {
            if (_explicitInterfaces != null)
            {
                return _explicitInterfaces;
            }
            if (IsDefault)
            {
                return _explicitInterfaces = ImmutableArray<MetaType>.Empty;
            }
            var directAncestor = await GetDirectAncestorAsync();
            var ancestors = directAncestor.IsDefault ? ImmutableHashSet<ISymbol>.Empty : directAncestor.TypeSymbol.AllInterfaces.ToImmutableHashSet(SymbolEqualityComparer.Default);
            return _explicitInterfaces = (await Task.WhenAll(
                    TypeSymbol.AllInterfaces
                        .Where(i => !SymbolEqualityComparer.Default.Equals(i, directAncestor?.TypeSymbol) && !ancestors.Contains(i))
                        .Select(SafeGetTypeAsync)))
                .ToImmutableArray();
        }

        public async Task<IEnumerable<MetaType>> GetDirectAncestorsAsync()
        {
            if (_ancestors != null)
            {
                return _ancestors;
            }
            var result = new List<MetaType>();
            var ancestor = await GetDirectAncestorAsync();
            while (!ancestor.IsDefault)
            {
                result.Add(ancestor);
                ancestor = await ancestor.GetDirectAncestorAsync();
            }
            return _ancestors = result.ToImmutableArray();
        }

        public TypedConstant GetAbstractJsonAttributeValue(INamedTypeSymbol typeSymbol)
        {
            var attribute = TypeSymbol?.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, typeSymbol));
            if (attribute != null)
            {
                return attribute.ConstructorArguments[0];
            }
            return default;
        }

        public async Task<bool> HasAncestorAsync()
        {
            return !(await GetDirectAncestorAsync()).IsDefault;
        }



        public bool IsAssignableFrom(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            return SymbolEqualityComparer.Default.Equals(type, TypeSymbol)
                || IsAssignableFrom(type.BaseType)
                || (TypeSymbol is INamedTypeSymbol namedType
                    && namedType.AllInterfaces.Any(s => SymbolEqualityComparer.Default.Equals(type, s)));
        }

        public override bool Equals(object obj)
        {
            if (obj is MetaType)
            {
                return Equals((MetaType)obj);
            }

            return false;
        }

        public bool Equals(MetaType other)
        {
            return SymbolEqualityComparer.Default.Equals(TypeSymbol, other.TypeSymbol);
        }

        public override int GetHashCode()
        {
            return TypeSymbol?.GetHashCode() ?? 0;
        }

        public static IEqualityComparer<MetaType> DefaultComparer = new Comparer();

        private class Comparer : IEqualityComparer<MetaType>
        {
            public bool Equals(MetaType x, MetaType y)
            {
                return SymbolEqualityComparer.Default.Equals(x.TypeSymbol, y.TypeSymbol);
            }

            public int GetHashCode(MetaType obj)
            {
                return obj.TypeSymbol?.GetHashCode() ?? 0;
            }
        }
    }
}