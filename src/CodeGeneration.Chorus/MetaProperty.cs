//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json.Serialization;
    using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

    [DebuggerDisplay("{Symbol?.Name}")]
    internal class MetaProperty
    {
        public static IEqualityComparer<MetaProperty> DefaultComparer = new Comparer();

        public static IEqualityComparer<MetaProperty> DefaultNameTypeComparer = new NameTypeComparer();

        private TypeSyntax _elementTypeSyntax;

        private TypeSyntax _typeSyntax;

        public MetaProperty(MetaType type, IPropertySymbol symbol)
        {
            MetaType = type;
            Symbol = symbol;
            _elementTypeSyntax = default;
            _typeSyntax = default;
            ElementType = GetTypeOrCollectionMemberType(Symbol.Type);
            if (ElementType.Name == "LogLevel")
            {
                ElementType = ElementType;
            }
            IsJsonSerializeable = IsElementTypeAssignableFrom(type.TransformationContext.JsonSerializeableType);
        }

        public ITypeSymbol ElementKeyType => GetDictionaryType(Symbol.Type)?.TypeArguments[0];

        public ITypeSymbol ElementType { get; }

        public TypeSyntax ElementTypeSyntax => _elementTypeSyntax ?? (_elementTypeSyntax = ElementType.GetFullyQualifiedSymbolName((Symbol as IArrayTypeSymbol)?.ElementNullableAnnotation ?? NullableAnnotation.None));

        public ITypeSymbol ElementValueType => GetDictionaryType(Symbol.Type)?.TypeArguments[1];

        public bool HasSetMethod => Symbol.SetMethod != null;

        public bool IsAbstract => Name == MetaType.AbstractJsonProperty;

        public bool IsCollection => IsCollectionType(Symbol.Type);

        public bool IsDefault => Symbol == null;

        public bool IsDefinitelyNotRecursive => Symbol.IsAttributeApplied<NotRecursiveAttribute>();

        public bool IsDictionary => IsDictionaryType(Symbol.Type);

        public bool IsIgnored => Symbol.IsPropertyIgnored();

        public bool IsJsonSerializeable { get; }

        public bool IsNullable => Symbol.IsPropertyNullable();
        public bool IsOptional => Symbol.IsPropertyNullable() || Symbol.Name == "PartitionKey";

        public bool IsReadonly => Symbol == null || Symbol.IsPropertyReadonly();

        public bool IsNonNullable => Symbol.IsPropertyNonNulable();

        public string JsonTypeName => IsJsonSerializeable ? "Object" : GetSafeTypeClassName(GetSafePropertyType(Symbol.Type));

        public MetaType MetaType { get; }

        public string Name => Symbol.Name;

        public IdentifierNameSyntax NameAsArgument
        {
            get
            {
                return IdentifierName(Symbol.Name.ToCamelCase());
            }
        }

        public IdentifierNameSyntax NameAsField
        {
            get
            {
                // Verify.Operation(!IsDefault, "Default instance.");
                return IdentifierName($"_{Symbol.Name.ToCamelCase()}");
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

        public IdentifierNameSyntax NameAsProperty => IdentifierName(Symbol.Name.ToPascalCase());

        public NullableAnnotation NullableAnnotation => Symbol.NullableAnnotation;

        public ExpressionStatementSyntax PropertyAssignment => ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(NameAsProperty.Identifier),
                        IdentifierName(NameAsArgument.Identifier)));

        public PropertyDeclarationSyntax PropertyDeclarationSyntax
        {
            get
            {
                if (Symbol.SetMethod != null && Symbol.GetMethod != null)
                {
                    return PropertyDeclaration(TypeSyntax, NameAsProperty.Identifier)
                            .AddModifiers(Token(SyntaxKind.PublicKeyword))
                            .AddAccessorListAccessors(
                                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                }
                else if (Symbol.SetMethod == null)
                {
                    return PropertyDeclaration(TypeSyntax, NameAsProperty.Identifier)
                            .AddModifiers(Token(SyntaxKind.PublicKeyword))
                            .AddAccessorListAccessors(
                                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))/*,
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)).AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))*/);
                }
                return PropertyDeclaration(TypeSyntax, NameAsProperty.Identifier)
                        .AddModifiers(Token(SyntaxKind.PublicKeyword))
                        .AddAccessorListAccessors(
                            /*SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)).AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)),*/
                            AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
            }
        }

        public ITypeSymbol SafeType => GetSafePropertyType(Symbol?.Type);

        public IPropertySymbol Symbol { get; }

        public ITypeSymbol Type => Symbol?.Type;

        public string TypeClassName => GetSafeTypeClassName(GetSafePropertyType(Symbol.Type));

        public string TypeName => GetSafeTypeName(Symbol.Type);

        public TypeSyntax TypeSyntax => _typeSyntax ?? (_typeSyntax = Type.GetFullyQualifiedSymbolName(Symbol.NullableAnnotation));

        public PropertyDeclarationSyntax ArrowPropertyDeclarationSyntax(ExpressionSyntax valueSyntax)
        {
            return PropertyDeclaration(TypeSyntax, NameAsProperty.Identifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .WithExpressionBody(ArrowExpressionClause(Token(SyntaxKind.EqualsGreaterThanToken), valueSyntax))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        public InvocationExpressionSyntax GetJsonValue(IdentifierNameSyntax paramName)
        {
            return InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, paramName, GetJsonValueName())
                       .WithOperatorToken(Token(SyntaxKind.DotToken)))
                       .WithArgumentList(
                           ArgumentList(SingletonSeparatedList(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(TriviaList(), NameAsJsonProperty, string.Empty, TriviaList())))))
                               .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                               .WithCloseParenToken(Token(SyntaxKind.CloseParenToken))
                           );
        }

        public IdentifierNameSyntax GetJsonValueName()
        {
            var nullable = IsNullable ? "Try" : "";
            var array = IsCollection ? "Array" : "";
            var typeName = TypeClassName;
            return IdentifierName($"{nullable}Get{typeName}{array}");
        }

        public bool IsAssignableFrom(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            var that = this;
            return SymbolEqualityComparer.Default.Equals(type, Symbol.Type)
                || Symbol.Type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(type, i))
                || type.Interfaces.Any(i => that.IsAssignableFrom(i));
        }

        public bool IsElementTypeAssignableFrom(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            var that = this;
            return SymbolEqualityComparer.Default.Equals(type, ElementType)
                || ElementType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(type, i))
                || IsAssignableFrom(type.BaseType);
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

        private static string GetSafeTypeClassName(ITypeSymbol t) => t.TypeKind == TypeKind.Interface ? t.Name.Substring(1) : t.Name;

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

        private static ITypeSymbol GetTypeOrCollectionMemberType(ITypeSymbol collectionOrOtherType)
        {
            if (TryGetCollectionElementType(collectionOrOtherType, out var memberType))
            {
                return memberType;
            }

            return collectionOrOtherType;
        }

        private static bool IsCollectionType(ITypeSymbol type) => GetCollectionType(type) != null;

        private static bool IsDictionaryType(ITypeSymbol type) => GetDictionaryType(type) != null;

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
