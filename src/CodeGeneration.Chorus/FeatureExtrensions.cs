namespace CodeGeneration.Chorus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal static class FeatureExtrensions
    {
        public static ConstructorDeclarationSyntax GetMeaningfulConstructor(this TypeDeclarationSyntax applyTo)
        {
            return applyTo.Members.OfType<ConstructorDeclarationSyntax>()
                .Where(ctor => !ctor.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))).Single();
        }

        public static bool IsFieldRequired(this IFieldSymbol fieldSymbol) => fieldSymbol.NullableAnnotation == NullableAnnotation.None;
        public static bool IsPropertyRequired(this IPropertySymbol fieldSymbol) => fieldSymbol.NullableAnnotation == NullableAnnotation.None;
        public static bool IsPropertyNullable(this IPropertySymbol fieldSymbol) => fieldSymbol.NullableAnnotation == NullableAnnotation.None;

        public static int GetFieldGeneration(this IFieldSymbol fieldSymbol)
        {
            var attribute = fieldSymbol?.GetAttributes()
                .SingleOrDefault(a => IsOrDerivesFrom<GenerateClassAttribute>(a.AttributeClass));
            return (int?)attribute?.ConstructorArguments.Single().Value ?? 0;
        }

        public static int GetPropertyGeneration(this IPropertySymbol fieldSymbol)
        {
            var attribute = fieldSymbol?.GetAttributes()
                .SingleOrDefault(a => IsOrDerivesFrom<GenerateClassAttribute>(a.AttributeClass));
            return (int?)attribute?.ConstructorArguments.Single().Value ?? 0;
        }

        public static bool IsFieldIgnored(this IFieldSymbol fieldSymbol)
        {
            if (fieldSymbol != null)
            {
                return fieldSymbol.IsStatic || fieldSymbol.IsImplicitlyDeclared || IsAttributeApplied<System.Text.Json.Serialization.JsonIgnoreAttribute>(fieldSymbol);
            }
            return false;
        }

        public static bool IsPropertyIgnored(this IPropertySymbol propertySymbol)
        {
            if (propertySymbol != null)
            {
                return propertySymbol.IsStatic || propertySymbol.IsImplicitlyDeclared || IsAttributeApplied<System.Text.Json.Serialization.JsonIgnoreAttribute>(propertySymbol);
            }
            return false;
        }

        public static bool IsPropertyReadonly(this IPropertySymbol propertySymbol)
        {
            if (propertySymbol != null)
            {
                return IsAttributeApplied<JsonReadOnlyAttribute>(propertySymbol);
            }
            return false;
        }

        public static bool IsAttributeApplied<T>(this ISymbol symbol) where T : Attribute
        {
            return symbol?.GetAttributes().Any(a => IsOrDerivesFrom<T>(a.AttributeClass)) ?? false;
        }

        public static bool IsOrDerivesFrom<T>(this INamedTypeSymbol type)
        {
            if (type != null)
            {
                if (type.Name == typeof(T).Name)
                {
                    // Don't sweat accuracy too much at this point.
                    return true;
                }

                return IsOrDerivesFrom<T>(type.BaseType);
            }

            return false;
        }

        public static bool IsAttribute<T>(this INamedTypeSymbol type)
        {
            if (type != null)
            {
                if (type.Name == typeof(T).Name)
                {
                    // Don't sweat accuracy too much at this point.
                    return true;
                }

                return IsAttribute<T>(type.BaseType);
            }

            return false;
        }

        public static bool HasAttribute<T>(this INamedTypeSymbol type)
        {
            return type?.GetAttributes().Any(a => IsAttribute<T>(a.AttributeClass)) ?? false;
        }

        public static NameSyntax GetFullyQualifiedSymbolName(this INamedTypeSymbol typeSymbol, NullableAnnotation nullableAnnotation = NullableAnnotation.None)
        {
            return (NameSyntax)GetFullyQualifiedSymbolName((INamespaceOrTypeSymbol)typeSymbol, nullableAnnotation);
        }

        public static TypeSyntax GetFullyQualifiedSymbolName(this IArrayTypeSymbol symbol, NullableAnnotation nullableAnnotation)
        {
            var elementType = GetFullyQualifiedSymbolName(symbol.ElementType, symbol.ElementNullableAnnotation);
            TypeSyntax result = SyntaxFactory.ArrayType(elementType)
                .AddRankSpecifiers(SyntaxFactory.ArrayRankSpecifier()
                    .AddSizes(SyntaxFactory.OmittedArraySizeExpression()));

            return nullableAnnotation == NullableAnnotation.Annotated ? SyntaxFactory.NullableType(result) : result;
        }


        public static TypeSyntax GetFullyQualifiedSymbolName(this INamespaceOrTypeSymbol symbol, NullableAnnotation nullableAnnotation)
        {
            if (symbol == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(symbol.Name))
            {
                if (symbol is IArrayTypeSymbol arrayTypeSymbol)
                {
                    var r = SyntaxFactory.ArrayType(arrayTypeSymbol.GetFullyQualifiedSymbolName(arrayTypeSymbol.ElementNullableAnnotation));
                    return nullableAnnotation == NullableAnnotation.Annotated ? (TypeSyntax)SyntaxFactory.NullableType(r) : r;
                }
                return null;
            }

            var parent = GetFullyQualifiedSymbolName(symbol.ContainingSymbol as INamespaceOrTypeSymbol, NullableAnnotation.None) as NameSyntax;
            SimpleNameSyntax leafName = SyntaxFactory.IdentifierName(symbol.Name);
            var typeSymbol = symbol as INamedTypeSymbol;
            if (typeSymbol != null && typeSymbol.IsGenericType)
            {
                var arguments = typeSymbol.TypeArguments.ToArray();
                var nullable = typeSymbol.TypeArgumentNullableAnnotations.ToArray();
                var args = new List<TypeSyntax>();
                for (var i = 0; i < arguments.Length; i++)
                {
                    args.Add(GetFullyQualifiedSymbolName(arguments[i], nullable[i]));
                }

                leafName = SyntaxFactory.GenericName(symbol.Name)
                    .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, args)));
            }

            TypeSyntax result = parent != null ? (NameSyntax)SyntaxFactory.QualifiedName(parent, leafName) : leafName;
            return typeSymbol?.TypeKind == TypeKind.Class && nullableAnnotation == NullableAnnotation.Annotated ? SyntaxFactory.NullableType(result) : result;
        }

        public static SyntaxToken[] GetModifiersForAccessibility(this INamedTypeSymbol template)
        {
            switch (template.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    return new[] { SyntaxFactory.Token(SyntaxKind.PublicKeyword) };
                case Accessibility.Protected:
                    return new[] { SyntaxFactory.Token(SyntaxKind.ProtectedKeyword) };
                case Accessibility.Internal:
                    return new[] { SyntaxFactory.Token(SyntaxKind.InternalKeyword) };
                case Accessibility.ProtectedOrInternal:
                    return new[] { SyntaxFactory.Token(SyntaxKind.ProtectedKeyword), SyntaxFactory.Token(SyntaxKind.InternalKeyword) };
                case Accessibility.Private:
                    return new[] { SyntaxFactory.Token(SyntaxKind.PrivateKeyword) };
                default:
                    throw new NotSupportedException();
            }
        }

        // public static QualifiedNameSyntax CreateIRecursiveParentOfTSyntax(this TypeSyntax recursiveType)
        // {
        //    return SyntaxFactory.QualifiedName(
        //        SyntaxFactory.IdentifierName(nameof(ImmutableObjectGraph)),
        //        SyntaxFactory.GenericName(
        //            SyntaxFactory.Identifier(nameof(IRecursiveParent<IRecursiveType>)),
        //            SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList<TypeSyntax>(recursiveType))));
        // }

    }


}