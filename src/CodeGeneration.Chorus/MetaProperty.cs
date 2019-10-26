namespace CodeGeneration.Chorus
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System.Text.Json.Serialization;

    internal struct MetaProperty
    {
        private readonly MetaType metaType;
        private TypeSyntax _elementTypeSyntax;
        private TypeSyntax _typeSyntax;

        public MetaProperty(MetaType type, IPropertySymbol symbol)
        {
            metaType = type;
            Symbol = symbol;
            _elementTypeSyntax = default;
            _typeSyntax = default;
        }

        public string Name => Symbol.Name;

        public IdentifierNameSyntax NameAsProperty => SyntaxFactory.IdentifierName(Symbol.Name.ToPascalCase());


        public IdentifierNameSyntax NameAsField
        {
            get
            {
                // Verify.Operation(!IsDefault, "Default instance.");
                return SyntaxFactory.IdentifierName($"_{Symbol.Name.ToCamelCase()}");
            }
        }

        public string NameAsJsonProperty
        {
            get
            {
                var jsonAttribute = Symbol?.GetAttributes().FirstOrDefault(a => a.AttributeClass.IsOrDerivesFrom<JsonPropertyNameAttribute>());
                if (jsonAttribute != null)
                {
                    var name = jsonAttribute.ConstructorArguments[0].ToCSharpString();
                    return name;
                }
                // Verify.Operation(!IsDefault, "Default instance.");
                return Symbol.Name.ToCamelCase();
            }
        }

        public IdentifierNameSyntax NameAsArgument
        {
            get
            {
                // Verify.Operation(!IsDefault, "Default instance.");
                return SyntaxFactory.IdentifierName(Symbol.Name.ToCamelCase());
            }
        }

        public ITypeSymbol Type => Symbol?.Type;

        public TypeSyntax TypeSyntax => _typeSyntax ?? (_typeSyntax = Type.GetFullyQualifiedSymbolName(Symbol.NullableAnnotation));

        public bool IsGeneratedImmutableType => !TypeAsGeneratedImmutable.IsDefault;

        public MetaType TypeAsGeneratedImmutable
        {
            get
            {
                return Type.IsAttributeApplied<GenerateClassAttribute>()
                    ? new MetaType(metaType.Generator, (INamedTypeSymbol)Type)
                    : default;
            }
        }

        public bool IsRequired => Symbol.IsPropertyRequired();
        public bool IsNullable => Symbol.IsPropertyNullable();
        public bool IsReadonly => Symbol == null || Symbol.IsPropertyReadonly();
        public bool IsIgnored => Symbol.IsPropertyIgnored();

        public int Generation => Symbol.GetPropertyGeneration();

        public bool IsCollection => IsCollectionType(Symbol.Type);

        public bool IsDictionary => IsDictionaryType(Symbol.Type);

        public string TypeName => GetSafeTypeName(Symbol.Type);

        public MetaType DeclaringType
        {
            get { return new MetaType(metaType.Generator, Symbol.ContainingType); }
        }

        public bool IsRecursiveCollection
        {
            get { return IsCollection && !DeclaringType.RecursiveType.IsDefault && SymbolEqualityComparer.Default.Equals(ElementType, DeclaringType.RecursiveType.TypeSymbol); }
        }

        public bool IsDefinitelyNotRecursive => Symbol.IsAttributeApplied<NotRecursiveAttribute>();

        /// <summary>
        /// Gets a value indicating whether this field is defined on the template type
        /// (as opposed to a base type).
        /// </summary>
        // public bool IsLocallyDefined
        // {
        //    get { return Symbol.ContainingType == metaType.Generator.applyToSymbol; }
        // }

        public NullableAnnotation NullableAnnotation => Symbol.NullableAnnotation;

        public IPropertySymbol Symbol { get; }

        public ITypeSymbol ElementType => GetTypeOrCollectionMemberType(Symbol.Type);

        public ITypeSymbol ElementKeyType => GetDictionaryType(Symbol.Type)?.TypeArguments[0];

        public ITypeSymbol ElementValueType => GetDictionaryType(Symbol.Type)?.TypeArguments[1];

        public TypeSyntax ElementTypeSyntax => _elementTypeSyntax ?? (_elementTypeSyntax = ElementType.GetFullyQualifiedSymbolName((Symbol as IArrayTypeSymbol)?.ElementNullableAnnotation ?? NullableAnnotation.None));

        public bool IsDefault => Symbol == null;


        public bool IsAssignableFrom(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            var that = this;
            return SymbolEqualityComparer.Default.Equals(type, Symbol.Type)
                || IsAssignableFrom(type.BaseType)
                || type.Interfaces.Any(i => that.IsAssignableFrom(i));
        }

        // public PropertyDeclarationSyntax DedfaultPropertyDeclarationSyntax(TypedConstant defaultValue)
        // {
        //    return SyntaxFactory.PropertyDeclaration(TypeSyntax, NameAsProperty.Identifier)
        //            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
        //            .AddAccessorListAccessors(
        //                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
        //                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(thisDotChildren))));
        // }
        public PropertyDeclarationSyntax PropertyDeclarationSyntax
        {
            get
            {
                if (Symbol.SetMethod != null && Symbol.GetMethod != null)
                {
                    return SyntaxFactory.PropertyDeclaration(TypeSyntax, NameAsProperty.Identifier)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .AddAccessorListAccessors(
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                }
                else if (Symbol.SetMethod == null)
                {
                    return SyntaxFactory.PropertyDeclaration(TypeSyntax, NameAsProperty.Identifier)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .AddAccessorListAccessors(
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))/*,
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)).AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))*/);
                }
                return SyntaxFactory.PropertyDeclaration(TypeSyntax, NameAsProperty.Identifier)
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                        .AddAccessorListAccessors(
                            /*SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)).AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)),*/
                            SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            }
        }

        private static ITypeSymbol GetTypeOrCollectionMemberType(ITypeSymbol collectionOrOtherType)
        {
            if (TryGetCollectionElementType(collectionOrOtherType, out var memberType))
            {
                return memberType;
            }

            return collectionOrOtherType;
        }

        private static bool TryGetCollectionElementType(ITypeSymbol collectionType, out ITypeSymbol elementType)
        {
            collectionType = GetCollectionType(collectionType);
            var arrayType = collectionType as IArrayTypeSymbol;
            if (arrayType != null)
            {
                elementType = arrayType.ElementType;
                return true;
            }

            var namedType = collectionType as INamedTypeSymbol;
            if (namedType != null)
            {
                if (namedType.IsGenericType && namedType.TypeArguments.Length == 1)
                {
                    elementType = namedType.TypeArguments[0];
                    return true;
                }
            }

            elementType = null;
            return false;
        }

        private static ITypeSymbol GetCollectionType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol)
            {
                return type;
            }

            var namedType = type as INamedTypeSymbol;
            if (namedType != null)
            {
                if (namedType.IsGenericType && namedType.TypeArguments.Length == 1)
                {
                    var collectionType = namedType.AllInterfaces.FirstOrDefault(i => i.Name == nameof(IReadOnlyCollection<int>));
                    if (collectionType != null)
                    {
                        return collectionType;
                    }
                }
            }

            return null;
        }
        private static string GetSafeTypeName(ITypeSymbol type)
        {

            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.IsGenericType && namedType.TypeArguments.Length == 1)
                {
                    return namedType.TypeArguments[0].Name;
                }
            }
            if (type is IArrayTypeSymbol arrayTypeSymbol)
            {
                return arrayTypeSymbol.ElementType.Name;
            }

            return type.Name;
        }


        private static INamedTypeSymbol GetDictionaryType(ITypeSymbol type)
        {
            var namedType = type as INamedTypeSymbol;
            if (namedType != null)
            {
                if (namedType.IsGenericType && namedType.TypeArguments.Length == 2)
                {
                    var collectionType = namedType.AllInterfaces.FirstOrDefault(i => i.Name == nameof(IImmutableDictionary<int, int>));
                    if (collectionType != null)
                    {
                        return collectionType;
                    }
                }
            }

            return null;
        }

        private static bool IsCollectionType(ITypeSymbol type) => GetCollectionType(type) != null;

        private static bool IsDictionaryType(ITypeSymbol type) => GetDictionaryType(type) != null;
    }

}