namespace CodeGeneration.Chorus
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System.Text.Json.Serialization;
    using System.Diagnostics;

    [DebuggerDisplay("{Symbol?.Name}")]
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
                    return jsonAttribute.ConstructorArguments[0].ToCSharpString();
                }
                return $"\"{Symbol.Name.ToCamelCase()}\"";

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
        public ITypeSymbol SafeType => GetSafePropertyType(Symbol?.Type);

        public TypeSyntax TypeSyntax => _typeSyntax ?? (_typeSyntax = Type.GetFullyQualifiedSymbolName(Symbol.NullableAnnotation));

        public bool IsAbstract => Name == metaType.AbstractJsonProperty;
        public bool IsRequired => Symbol.IsPropertyRequired();
        public bool IsNullable => Symbol.IsPropertyNullable();
        public bool IsReadonly => Symbol == null || Symbol.IsPropertyReadonly();
        public bool IsIgnored => Symbol.IsPropertyIgnored();
        public bool HasSetMethod => Symbol.SetMethod != null;
        public bool IsCollection => IsCollectionType(Symbol.Type);
        public bool IsDictionary => IsDictionaryType(Symbol.Type);
        public string TypeName => GetSafeTypeName(Symbol.Type);
        public string TypeClassName => GetSafeTypeClassName(GetSafePropertyType(Symbol.Type));

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

        public PropertyDeclarationSyntax ArrowPropertyDeclarationSyntax(ExpressionSyntax valueSyntax)
        {
            return SyntaxFactory.PropertyDeclaration(TypeSyntax, NameAsProperty.Identifier)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken), valueSyntax))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

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

        private static string GetSafeTypeClassName(ITypeSymbol t) => t.TypeKind == TypeKind.Interface ? t.Name.Substring(1) : t.Name;

        private static ITypeSymbol GetSafePropertyType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.IsGenericType && namedType.TypeArguments.Length == 1)
                {
                    return namedType.TypeArguments[0];
                }
            }
            if (type is IArrayTypeSymbol arrayTypeSymbol)
            {
                return arrayTypeSymbol.ElementType;
            }

            return type;
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

        public static IEqualityComparer<MetaProperty> DefaultComparer = new Comparer();
        public static IEqualityComparer<MetaProperty> DefaultNameTypeComparer = new NameTypeComparer();

        private class Comparer : IEqualityComparer<MetaProperty>
        {
            public bool Equals(MetaProperty x, MetaProperty y)
            {
                return x.Symbol.Equals(y.Symbol);
            }

            public int GetHashCode(MetaProperty obj)
            {
                return obj.Symbol.GetHashCode();
            }
        }

        private class NameTypeComparer : IEqualityComparer<MetaProperty>
        {
            public bool Equals(MetaProperty x, MetaProperty y)
            {
                return x.Symbol.Name.Equals(y.Symbol.Name) && x.Symbol.Type.Equals(y.Symbol.Type);
            }

            public int GetHashCode(MetaProperty obj)
            {
                unchecked
                {
                    return 17 * (23 + obj.Symbol.Name.GetHashCode()) * (23 + obj.Symbol.Type.GetHashCode());
                }
            }
        }

    }

}