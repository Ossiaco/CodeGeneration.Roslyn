//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Editing;
    using Microsoft.CodeAnalysis.PooledObjects;
    using Microsoft.CodeAnalysis.Text;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

    internal static class Syntax
    {
        internal static SyntaxGenerator Generator = SyntaxGenerator.GetGenerator(new AdhocWorkspace(), LanguageNames.CSharp);

        internal static MethodDeclarationSyntax AddNewKeyword(MethodDeclarationSyntax method)
        {
            return method.WithModifiers(method.Modifiers.Insert(0, Token(SyntaxKind.NewKeyword)));
        }

        internal static PropertyDeclarationSyntax AddNewKeyword(PropertyDeclarationSyntax method)
        {
            return method.WithModifiers(method.Modifiers.Insert(0, Token(SyntaxKind.NewKeyword)));
        }

        internal static IEnumerable<BaseTypeSyntax> AsBaseType(this InterfaceDeclarationSyntax value)
        {
            if (value.BaseList is BaseListSyntax baselist && baselist.Types[0].Type is IdentifierNameSyntax nameSyntax)
            {
                var baseClass = IdentifierName(Identifier(nameSyntax.Identifier.ValueText.Substring(1)));
                yield return SimpleBaseType(baseClass);
            }
            yield return SimpleBaseType(ParseName(value.Identifier.ValueText));
        }

        internal static IEnumerable<BaseTypeSyntax> AsFullyQualifiedBaseType(this SemanticModel model, MetaType metaType)
        {
            var value = (TypeDeclarationSyntax)metaType.DeclarationSyntax;
            var context = metaType.TransformationContext;
            if (value.BaseList is BaseListSyntax baselist && baselist.Types[0].Type is IdentifierNameSyntax nameSyntax)
            {
                var typeSymbol = ((INamedTypeSymbol)model.GetTypeInfo(nameSyntax).Type);
                if (!typeSymbol.Equals(context.JsonSerializeableType))
                {
                    SimpleNameSyntax leafName = IdentifierName(typeSymbol.Name.Substring(1));
                    TypeSyntax typeSyntax = (typeSymbol.ContainingSymbol as INamespaceOrTypeSymbol)?.GetFullyQualifiedSymbolName(NullableAnnotation.None) is NameSyntax parent ? (NameSyntax)QualifiedName(parent, leafName) : leafName;
                    yield return SimpleBaseType(typeSyntax);
                }
            }
            var interfaceType = (TypeSyntax)ParseName(value.Identifier.ValueText);
            var classType = (TypeSyntax)ParseName(metaType.ClassNameIdentifier.ValueText);
            var IEquatableType = Identifier("System.IEquatable");

            yield return SimpleBaseType(interfaceType);
            yield return SimpleBaseType(GenericName(IEquatableType, TypeArgumentList(SingletonSeparatedList(interfaceType))));
            yield return SimpleBaseType(GenericName(IEquatableType, TypeArgumentList(SingletonSeparatedList(classType))));
        }

        internal static MemberAccessExpressionSyntax BaseDot(SimpleNameSyntax memberAccess)
        {
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                BaseExpression(),
                memberAccess);
        }

        internal static ExpressionSyntax ChainBinaryExpressions(this IEnumerable<ExpressionSyntax> expressions, SyntaxKind binaryOperator)
        {
            return expressions.Aggregate((ExpressionSyntax)null, (agg, e) => agg != null ? BinaryExpression(binaryOperator, agg, e) : e);
        }

        internal static IdentifierNameSyntax ClassName(this InterfaceDeclarationSyntax value) => IdentifierName(Identifier(value.Identifier.ValueText.Substring(1)));

        internal static ImmutableArray<DeclarationInfo> ComputeDeclarationsInSpan(this SemanticModel model, TextSpan span, bool getSymbol, CancellationToken cancellationToken)
        {
            var declarationInfoBuilder = ArrayBuilder<DeclarationInfo>.GetInstance();
            CSharpDeclarationComputer.ComputeDeclarationsInSpan(model, span, getSymbol, declarationInfoBuilder, cancellationToken);
            return declarationInfoBuilder.ToImmutableAndFree();
        }

        internal static ExpressionSyntax CreateDictionary(TypeSyntax keyType, TypeSyntax valueType)
        {
            // System.Collections.Immutable.ImmutableDictionary.Create<TKey, TValue>()
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    GetTypeSyntax(typeof(ImmutableDictionary)),
                    GenericName(nameof(ImmutableDictionary.Create)).AddTypeArgumentListArguments(keyType, valueType)),
                ArgumentList());
        }

        internal static ExpressionSyntax CreateImmutableStack(TypeSyntax elementType = null)
        {
            var typeSyntax = QualifiedName(
                QualifiedName(
                    QualifiedName(
                        IdentifierName(nameof(System)),
                        IdentifierName(nameof(System.Collections))),
                    IdentifierName(nameof(System.Collections.Immutable))),
                IdentifierName(nameof(ImmutableStack)));

            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                typeSyntax,
                elementType == null ? (SimpleNameSyntax)IdentifierName(nameof(ImmutableStack.Create)) : GenericName(nameof(ImmutableStack.Create)).AddTypeArgumentListArguments(elementType));
        }

        internal static InvocationExpressionSyntax EnumerableExtension(SimpleNameSyntax linqMethod, ExpressionSyntax receiver, ArgumentListSyntax arguments)
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    GetTypeSyntax(typeof(Enumerable)),
                    linqMethod),
                arguments.PrependArgument(Argument(receiver)));
        }

        internal static NameSyntax FuncOf(params TypeSyntax[] typeArguments)
        {
            return QualifiedName(
                IdentifierName(nameof(System)),
                GenericName(nameof(Func<int>)).AddTypeArgumentListArguments(typeArguments));
        }

        internal static NameSyntax GetTypeSyntax(Type type)
        {
            SimpleNameSyntax leafType = IdentifierName(type.GetTypeInfo().IsGenericType ? type.Name.Substring(0, type.Name.IndexOf('`')) : type.Name);
            if (type.GetTypeInfo().IsGenericType)
            {
                leafType = GenericName(
                    ((IdentifierNameSyntax)leafType).Identifier,
                    TypeArgumentList(JoinSyntaxNodes<TypeSyntax>(SyntaxKind.CommaToken, type.GenericTypeArguments.Select(GetTypeSyntax))));
            }

            if (type.Namespace != null)
            {
                NameSyntax namespaceName = null;
                foreach (var segment in type.Namespace.Split('.'))
                {
                    var segmentName = IdentifierName(segment);
                    namespaceName = namespaceName == null
                        ? (NameSyntax)segmentName
                        : QualifiedName(namespaceName, IdentifierName(segment));
                }

                return QualifiedName(namespaceName, leafType);
            }

            return leafType;
        }

        internal static NameSyntax GetTypeSyntax(string ns, string leafType)
        {
            NameSyntax namespaceName = null;
            foreach (var segment in ns.Split('.'))
            {
                var segmentName = IdentifierName(segment);
                namespaceName = namespaceName == null
                    ? (NameSyntax)segmentName
                    : QualifiedName(namespaceName, IdentifierName(segment));
            }

            return QualifiedName(namespaceName, IdentifierName(leafType));
        }

        internal static NameSyntax IEnumerableOf(TypeSyntax typeSyntax)
        {
            return QualifiedName(
                QualifiedName(
                    QualifiedName(
                        IdentifierName(nameof(System)),
                        IdentifierName(nameof(System.Collections))),
                        IdentifierName(nameof(System.Collections.Generic))),
                GenericName(
                    Identifier(nameof(IEnumerable<int>)),
                    TypeArgumentList(SingletonSeparatedList(typeSyntax))));
        }

        internal static NameSyntax IEnumeratorOf(TypeSyntax typeSyntax)
        {
            return QualifiedName(
                QualifiedName(
                    QualifiedName(
                        IdentifierName(nameof(System)),
                        IdentifierName(nameof(System.Collections))),
                    IdentifierName(nameof(System.Collections.Generic))),
                GenericName(
                    Identifier(nameof(IEnumerator<int>)),
                    TypeArgumentList(SingletonSeparatedList(typeSyntax))));
        }

        internal static NameSyntax IEqualityComparerOf(TypeSyntax typeSyntax)
        {
            return QualifiedName(
                QualifiedName(
                    QualifiedName(
                        IdentifierName(nameof(System)),
                        IdentifierName(nameof(System.Collections))),
                    IdentifierName(nameof(System.Collections.Generic))),
                GenericName(
                    Identifier(nameof(IEqualityComparer<int>)),
                    TypeArgumentList(SingletonSeparatedList(typeSyntax))));
        }

        internal static NameSyntax IEquatableOf(TypeSyntax typeSyntax)
        {
            return QualifiedName(
                IdentifierName(nameof(System)),
                GenericName(
                    Identifier(nameof(IEquatable<int>)),
                    TypeArgumentList(SingletonSeparatedList(typeSyntax))));
        }

        internal static NameSyntax ImmutableStackOf(TypeSyntax typeSyntax)
        {
            return QualifiedName(
                QualifiedName(
                    QualifiedName(
                        IdentifierName(nameof(System)),
                        IdentifierName(nameof(System.Collections))),
                    IdentifierName(nameof(System.Collections.Immutable))),
                GenericName(
                    Identifier(nameof(ImmutableStack<int>)),
                    TypeArgumentList(SingletonSeparatedList(typeSyntax))));
        }

        internal static NameSyntax IReadOnlyCollectionOf(TypeSyntax elementType)
        {
            return QualifiedName(
                QualifiedName(
                    QualifiedName(
                        IdentifierName(nameof(System)),
                        IdentifierName(nameof(System.Collections))),
                    IdentifierName(nameof(System.Collections.Generic))),
                GenericName(
                    Identifier(nameof(IReadOnlyCollection<int>)),
                    TypeArgumentList(SingletonSeparatedList(elementType))));
        }

        internal static NameSyntax IReadOnlyListOf(TypeSyntax elementType)
        {
            return QualifiedName(
                QualifiedName(
                    QualifiedName(
                        IdentifierName(nameof(System)),
                        IdentifierName(nameof(System.Collections))),
                    IdentifierName(nameof(System.Collections.Generic))),
                GenericName(
                    Identifier(nameof(IReadOnlyList<int>)),
                    TypeArgumentList(SingletonSeparatedList(elementType))));
        }

        internal static SeparatedSyntaxList<T> JoinSyntaxNodes<T>(SyntaxKind tokenDelimiter, IEnumerable<T> nodes)
            where T : SyntaxNode
        {
            return SeparatedList<T>(JoinSyntaxNodes(Token(tokenDelimiter), nodes.ToArray()));
        }

        internal static SeparatedSyntaxList<T> JoinSyntaxNodes<T>(SyntaxKind tokenDelimiter, ImmutableArray<T> nodes)
            where T : SyntaxNode
        {
            return SeparatedList<T>(JoinSyntaxNodes(Token(tokenDelimiter), nodes));
        }

        internal static SeparatedSyntaxList<T> JoinSyntaxNodes<T>(SyntaxKind tokenDelimiter, params T[] nodes)
            where T : SyntaxNode
        {
            return SeparatedList<T>(JoinSyntaxNodes(Token(tokenDelimiter), nodes));
        }

        internal static SyntaxNodeOrTokenList JoinSyntaxNodes<T>(SyntaxToken separatingToken, IReadOnlyList<T> nodes)
            where T : SyntaxNode
        {
            // Requires.NotNull(nodes, nameof(nodes));

            switch (nodes.Count)
            {
                case 0:
                    return NodeOrTokenList();
                case 1:
                    return NodeOrTokenList(nodes[0]);
                default:
                    var nodesOrTokens = new SyntaxNodeOrToken[(nodes.Count * 2) - 1];
                    nodesOrTokens[0] = nodes[0];
                    for (var i = 1; i < nodes.Count; i++)
                    {
                        var targetIndex = i * 2;
                        nodesOrTokens[targetIndex - 1] = separatingToken;
                        nodesOrTokens[targetIndex] = nodes[i];
                    }

                    return NodeOrTokenList(nodesOrTokens);
            }
        }

        internal static NameSyntax KeyValuePairOf(TypeSyntax keyType, TypeSyntax valueType)
        {
            return QualifiedName(
                QualifiedName(
                    QualifiedName(
                        IdentifierName(nameof(System)),
                        IdentifierName(nameof(System.Collections))),
                    IdentifierName(nameof(System.Collections.Generic))),
                GenericName(
                    Identifier(nameof(KeyValuePair<int, int>)),
                    TypeArgumentList(JoinSyntaxNodes(SyntaxKind.CommaToken, keyType, valueType))));
        }

        internal static ArgumentListSyntax PrependArgument(this ArgumentListSyntax list, ArgumentSyntax argument)
        {
            return ArgumentList(SingletonSeparatedList(argument))
                .AddArguments(list.Arguments.ToArray());
        }

        internal static ParameterListSyntax PrependParameter(this ParameterListSyntax list, ParameterSyntax parameter)
        {
            return ParameterList(SingletonSeparatedList(parameter))
                .AddParameters(list.Parameters.ToArray());
        }

        internal static StatementSyntax RequiresNotNull(IdentifierNameSyntax parameter)
        {
            // if (other == null) { throw new System.ArgumentNullException(nameof(other)); }
            return IfStatement(
                BinaryExpression(SyntaxKind.EqualsExpression, parameter, LiteralExpression(SyntaxKind.NullLiteralExpression)),
                ThrowStatement(
                    ObjectCreationExpression(GetTypeSyntax(typeof(ArgumentNullException))).AddArgumentListArguments(
                        Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(parameter.Identifier.ToString()))))));
        }

        internal static ExpressionSyntax ThisDot(SimpleNameSyntax memberAccess)
        {
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ThisExpression(),
                memberAccess);
        }

        internal static InvocationExpressionSyntax ToList(ExpressionSyntax expression)
        {
            return InvocationExpression(
                // System.Linq.Enumerable.ToList
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    QualifiedName(
                        QualifiedName(
                            IdentifierName(nameof(System)),
                            IdentifierName(nameof(System.Linq))),
                        IdentifierName(nameof(Enumerable))),
                    IdentifierName(nameof(Enumerable.ToList))),
                ArgumentList(SingletonSeparatedList(Argument(expression))));
        }

        private static ExpressionSyntax GenerateNullLiteral() => LiteralExpression(SyntaxKind.NullLiteralExpression);
    }
}
