﻿//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

#nullable disable

namespace CodeGeneration.Chorus
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CodeGeneration.Chorus.Json;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    [DebuggerDisplay("{TypeSymbol?.Name}")]
    internal class MetaType
    {
        public static IEqualityComparer<MetaType> DefaultComparer = new Comparer();

        private ImmutableArray<PropertyDeclarationSyntax>? _abstractImplementations;
        private ImmutableArray<MetaType>? _allInterfaces;
        private ImmutableHashSet<MetaProperty> _allProperties;
        private ImmutableArray<MetaType>? _ancestors;
        private ImmutableArray<MetaType>? _descendents;
        private MetaType _directAncestor;
        private ImmutableArray<MetaType>? _explicitInterfaces;
        private bool? _hasChanged;
        private ImmutableHashSet<MetaProperty> _inheritedProperties;
        private bool? _isAbstractType;
        private bool? _isPartialClass;
        private ImmutableHashSet<MetaProperty> _localProperties;

        public MetaType(INamedTypeSymbol typeSymbol, BaseTypeDeclarationSyntax declarationSyntax, SemanticModel semanticModel, ITransformationContext context)
        {
            _localProperties = null;
            _allProperties = null;
            _inheritedProperties = null;
            _ancestors = null;
            _allInterfaces = null;
            _explicitInterfaces = null;
            _directAncestor = null;
            _descendents = null;
            _abstractImplementations = null;

            this.TransformationContext = context;
            TypeSymbol = typeSymbol;
            DeclarationSyntax = declarationSyntax;
            SemanticModel = semanticModel;
            //ISerializeable = IsAssignableFrom(CodeGen.IJsonSerializableType);

            this.IsIncompleteClass = typeSymbol?.GetMembers().OfType<IPropertySymbol>().Any(f => f.IsPropertyIgnored()) ?? true;
            var codeGenAttribute = typeSymbol?.GetAttributes().FirstOrDefault(a => a.AttributeClass.IsOrDerivesFrom<GenerateClassAttribute>());
            if (codeGenAttribute != null)
            {
                var attribute = codeGenAttribute.NamedArguments.FirstOrDefault(v => v.Key == nameof(GenerateClassAttribute.AbstractAttributeType)).Value;
                var field = codeGenAttribute.NamedArguments.FirstOrDefault(v => v.Key == nameof(GenerateClassAttribute.AbstractField)).Value;
                AbstractAttribute = (INamedTypeSymbol)attribute.Value;
                AbstractJsonProperty = (string)field.Value;
                HasAbstractJsonProperty = (AbstractAttribute != null && AbstractJsonProperty != null);
            }
            else
            {
                AbstractJsonProperty = null;
                AbstractAttribute = null;
                HasAbstractJsonProperty = false;
            }

            var stringEnumAttribute = typeSymbol?.GetAttributes().FirstOrDefault(a => a.AttributeClass.IsOrDerivesFrom<JsonStringEnumAttribute>());
            if (stringEnumAttribute != null)
            {
                IsEnumAsString = true;
                JsonStringEnumFormat = (JsonStringEnumFormat)stringEnumAttribute.ConstructorArguments[0].Value;
            }

            if (declarationSyntax != null)
            {
                var fi = Path.Combine(context.IntermediateOutputDirectory, declarationSyntax.SyntaxTree.FilePath[context.RootLength..]);
                OutputFilePath = Path.Combine(Path.GetDirectoryName(fi), Path.GetFileNameWithoutExtension(fi) + $".generated.cs");
            }

            IsJsonSerializable = IsAssignableFrom(TransformationContext.JsonSerializeableType);
            if (typeSymbol != null)
            {
                try
                {
                    var equatableType = TransformationContext.IIEquatableType.Construct(typeSymbol);
                    IsIEquatable = typeSymbol.AllInterfaces.Contains(equatableType);
                }
                catch (ArgumentException e) when (string.Compare(e.Message, "Syntax node is not within syntax tree") == 0)
                {
                    IsIEquatable = false;
                }
            }
            else
            {
                IsIEquatable = false;
            }
        }

        public bool IsIEquatable { get; } = false;

        public INamedTypeSymbol AbstractAttribute { get; }

        public string AbstractJsonProperty { get; }

        public IdentifierNameSyntax ClassName => (DeclarationSyntax as InterfaceDeclarationSyntax)?.ClassName() ?? SyntaxFactory.IdentifierName(DeclarationSyntax.Identifier);

        public SyntaxToken ClassNameIdentifier => ClassName.Identifier;

        public INamespaceSymbol ContainingNamespace
        {
            get { return TypeSymbol.ContainingNamespace; }
        }

        public BaseTypeDeclarationSyntax DeclarationSyntax { get; }

        public IdentifierNameSyntax FullyQualifiedClassName => (DeclarationSyntax is InterfaceDeclarationSyntax)
            ? SyntaxFactory.IdentifierName(SyntaxFactory.Identifier($"{TypeSymbol.ContainingNamespace}.{TypeSymbol.Name[1..]}"))
            : SyntaxFactory.IdentifierName(SyntaxFactory.Identifier($"{TypeSymbol.ContainingNamespace}.{TypeSymbol.Name}"));

        public bool HasAbstractJsonProperty { get; }

        public SyntaxToken InterfaceNameIdentifier => DeclarationSyntax.Identifier;

        public bool IsDefault
        {
            get { return TypeSymbol == null; }
        }

        public bool IsEnumAsString { get; }

        public bool IsGenericType => TypeSymbol.IsGenericType;

        public bool IsIncompleteClass { get; }

        public bool IsJsonSerializable { get; }

        public bool IsPartialClass => ((bool?)(_isPartialClass ??= TypeSymbol.IsReferenceType && (DeclarationSyntax?.Modifiers.Any(SyntaxKind.PartialKeyword) ?? false))).Value;

        public JsonStringEnumFormat JsonStringEnumFormat { get; }

        public NameSyntax Namespace => DeclarationSyntax.Ancestors().OfType<NamespaceDeclarationSyntax>().Single().Name.WithoutTrivia();

        public string OutputFilePath { get; }

        public SemanticModel SemanticModel { get; }

        public string SourceFilePath => DeclarationSyntax.SyntaxTree.FilePath;

        public ITransformationContext TransformationContext { get; }

        public INamedTypeSymbol TypeSymbol { get; private set; }

        public NameSyntax TypeSyntax
        {
            get { return TypeSymbol.GetFullyQualifiedSymbolName(); }
        }

        public bool Equals(MetaType other)
        {
            return SymbolEqualityComparer.Default.Equals(TypeSymbol, other.TypeSymbol);
        }

        public override bool Equals(object obj)
        {
            if (obj is MetaType type)
            {
                return Equals(type);
            }

            return false;
        }

        public TypedConstant GetAbstractJsonAttributeValue(INamedTypeSymbol typeSymbol)
        {
            var attribute = TypeSymbol?.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, typeSymbol));
            if (attribute != null)
            {
                if (attribute.ConstructorArguments.Length == 1)
                {
                    return attribute.ConstructorArguments[0];
                }
                throw new InvalidDataException($"The abstract type resolver {typeSymbol.Name} for {TypeSymbol?.Name} could does not exist or is invalid");
            }
            return default;
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

        public async Task<MetaType> GetDirectAncestorAsync()
        {
            if (_directAncestor != null)
            {
                return _directAncestor;
            }
            if (DeclarationSyntax?.BaseList is BaseListSyntax baselist && baselist.Types[0].Type is IdentifierNameSyntax nameSyntax)
            {
                var symbol = (INamedTypeSymbol)SemanticModel.GetTypeInfo(nameSyntax).Type;
                if (!SymbolEqualityComparer.Default.Equals(symbol, TransformationContext.JsonSerializeableType))
                {
                    return _directAncestor = await SafeGetTypeAsync(symbol);
                }
            }
            return _directAncestor = new MetaType(null, null, SemanticModel, TransformationContext);
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

        public async Task<ImmutableArray<MetaType>> GetDirectDescendentsAsync()
        {
            if (_descendents != null)
            {
                return _descendents.Value;
            }

            var results = await Task.WhenAll(TransformationContext.AllNamedTypeSymbols.Values.Select(async (mt) => this.Equals(await mt.GetDirectAncestorAsync()) ? mt : null));
            _descendents = results.Where(v => v != null).ToImmutableArray();
            return _descendents.Value;
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

        public override int GetHashCode()
        {
            return TypeSymbol?.GetHashCode() ?? 0;
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

        public async Task<ImmutableArray<PropertyDeclarationSyntax>> GetPropertyOverridesAsync()
        {
            if (_abstractImplementations != null)
            {
                return _abstractImplementations.Value;
            }

            static async Task<bool> FindAbstractImplementationAsync(MetaType probe, MetaType ancestor)
            {
                probe = await probe.GetDirectAncestorAsync();
                if (probe.Equals(ancestor))
                {
                    return false;
                }
                if (probe?.GetAbstractJsonAttributeValue(ancestor.AbstractAttribute).Kind == TypedConstantKind.Error)
                {
                    return await FindAbstractImplementationAsync(probe, ancestor);
                }
                return probe != null;
            }

            async Task<(bool implemented, PropertyDeclarationSyntax prop)> GetAbstractPropertyAsync(MetaType ancestor)
            {
                var constValue = GetAbstractJsonAttributeValue(ancestor.AbstractAttribute);
                if (constValue.Kind != TypedConstantKind.Error)
                {
                    var expr = CodeGen.FormatValue(constValue);
                    var inheritedProperties = await GetInheritedPropertiesAsync();
                    var inheritedProperty = inheritedProperties.SingleOrDefault(p => p.Name == ancestor.AbstractJsonProperty);
                    var property = inheritedProperty.ArrowPropertyDeclarationSyntax(expr)
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
                    return (true, property);
                }
                var implemented = await FindAbstractImplementationAsync(this, ancestor);
                return (implemented, null);
            }

            // Select out the fields used to serialize an abstract derived types
            var inheritedMembers = (await GetDirectAncestorsAsync())
                .Where(a => a.HasAbstractJsonProperty)
                .ToImmutableArray();

            var implementations = await Task.WhenAll(inheritedMembers.Select(GetAbstractPropertyAsync));
            var implemented = implementations.Count(v => v.implemented == true);

            _abstractImplementations = implementations
                .Where(v => v.prop != null)
                .Select(v => v.prop)
                .ToImmutableArray();

            _isAbstractType = HasAbstractJsonProperty || implemented < inheritedMembers.Length;

            return _abstractImplementations.Value;
        }

        public async Task<HashSet<MetaType>> GetRecursiveDescendentsAsync(HashSet<MetaType> values = null)
        {
            values ??= new HashSet<MetaType>(MetaType.DefaultComparer);
            await GetPropertyOverridesAsync();
            var descendents = await GetDirectDescendentsAsync();
            foreach (var descendent in descendents)
            {
                values.Add(descendent);
                await descendent.GetRecursiveDescendentsAsync(values);
            }
            return values;
        }

        public async Task<bool> HasAncestorAsync() => !(await GetDirectAncestorAsync()).IsDefault;

        public bool HasChanged()
        {
            bool EvaluateChanges()
            {
                if (string.IsNullOrEmpty(OutputFilePath))
                {
                    return false;
                }
                if (!File.Exists(OutputFilePath) || File.GetLastWriteTime(SourceFilePath) > File.GetLastWriteTime(OutputFilePath))
                {
                    return true;
                }
                return false;
            }

            return _hasChanged ?? (_hasChanged = EvaluateChanges()).Value;
        }

        public async Task<bool> HasDescendentsAsync() => (await GetDirectDescendentsAsync()).Length > 0;

        public async Task<bool> IsAbstractTypeAsync()
        {
            if (_isAbstractType != null)
            {
                return _isAbstractType.Value;
            }

            static async Task<bool> FindAbstractImplementationAsync(MetaType probe, MetaType ancestor)
            {
                probe = await probe.GetDirectAncestorAsync();
                if (probe.Equals(ancestor))
                {
                    return false;
                }
                if (probe?.GetAbstractJsonAttributeValue(ancestor.AbstractAttribute).Kind == TypedConstantKind.Error)
                {
                    return await FindAbstractImplementationAsync(probe, ancestor);
                }
                return probe != null;
            }

            async Task<bool> GetAbstractPropertyAsync(MetaType ancestor)
            {
                var constValue = GetAbstractJsonAttributeValue(ancestor.AbstractAttribute);
                if (constValue.Kind != TypedConstantKind.Error)
                {
                    return true;
                }
                return await FindAbstractImplementationAsync(this, ancestor);
            }

            async Task<bool> EvaluateAbstractTypeAsync()
            {
                // Select out the fields used to serialize an abstract derived types
                var inheritedMembers = (await GetDirectAncestorsAsync())
                    .Where(a => a.HasAbstractJsonProperty)
                    .ToImmutableArray();

                var implementations = await Task.WhenAll(inheritedMembers.Select(GetAbstractPropertyAsync));
                var implemented = implementations.Count(v => v == true);


                return implemented < inheritedMembers.Length;
            }

            return (_isAbstractType = HasAbstractJsonProperty || (await EvaluateAbstractTypeAsync())).Value;
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

        private async Task<MetaType> SafeGetTypeAsync(INamedTypeSymbol symbol)
        {
            async Task<MetaType> GetMetaTypeForSymbolAsync(INamedTypeSymbol typeSymbol)
            {
                if (typeSymbol.DeclaringSyntaxReferences.Length > 0)
                {
                    var syntax = await typeSymbol.DeclaringSyntaxReferences[0].GetSyntaxAsync();
                    var inputSemanticModel = SemanticModel.Compilation.GetSemanticModel(syntax.SyntaxTree);
                    return new MetaType(typeSymbol, syntax as BaseTypeDeclarationSyntax, inputSemanticModel, TransformationContext);
                }
                return new MetaType(typeSymbol, null, SemanticModel, TransformationContext);

            };
            if (symbol == null)
            {
                return new MetaType(symbol, null, SemanticModel, TransformationContext);
            }
            return TransformationContext.AllNamedTypeSymbols.TryGetValue(symbol, out var result) ? result : await GetMetaTypeForSymbolAsync(symbol);
        }

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
