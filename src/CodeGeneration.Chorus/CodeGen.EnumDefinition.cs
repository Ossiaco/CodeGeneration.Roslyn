﻿using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeGeneration.Chorus
{
    static partial class CodeGen
    {

        private static RichGenerationResult GenerateSerializerForEnum(MetaType metaType, CancellationToken cancellationToken)
        {
            var usings = SyntaxFactory.List<UsingDirectiveSyntax>();
            var members = new[] { CreateEnumSerializerClass(metaType) };
            return new RichGenerationResult() { Members = SyntaxFactory.List(members), Usings = usings };
        }

        private static MemberDeclarationSyntax CreateEnumSerializerClass(MetaType sourceMetaType)
        {

            var namespaceSyntax = sourceMetaType.DeclarationSyntax.Ancestors().OfType<NamespaceDeclarationSyntax>().Single().Name.WithoutTrivia();
            var identifierSyntax = IdentifierName(sourceMetaType.InterfaceNameIdentifier);

            var mergedFeatures = new List<FeatureGenerator>();
            mergedFeatures.Merge(new StyleCopCompliance(sourceMetaType));

            var className = $"{sourceMetaType.ClassNameIdentifier.Text}JsonSerializer";
            var namespaceName = ParseName(typeof(System.Text.Json.JsonElement).Namespace);

            var innerMembers = new List<MemberDeclarationSyntax>();

            if (sourceMetaType.IsEnumAsString)
            {
                innerMembers.AddRange(JsonGetEnumMethods(sourceMetaType, "GetEnumString", identifierSyntax));
                innerMembers.AddRange(JsonWriteEnumMethods(sourceMetaType, "WriteEnumString", identifierSyntax));
            }
            else
            {
                innerMembers.AddRange(JsonGetEnumMethods(sourceMetaType, "GetEnum", identifierSyntax));
                innerMembers.AddRange(JsonWriteEnumMethods(sourceMetaType, "WriteEnum", identifierSyntax));
            }

            var partialClass = ClassDeclaration(Identifier(className))
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithMembers(List(innerMembers));

            partialClass = mergedFeatures.
                Aggregate(partialClass, (acc, feature) => feature.ProcessApplyToClassDeclaration(acc));

            var declarations = List<MemberDeclarationSyntax>();
            declarations = declarations.Add(partialClass);
            var usingsDirectives = List(new[] {
                UsingDirective(ParseName(typeof(System.Buffers.IBufferWriter<byte>).Namespace)),
                UsingDirective(ParseName("Chorus.Common.Text.Json")),
                UsingDirective(namespaceSyntax),
                UsingDirective(ParseName(typeof(IEnumerable<string>).Namespace))
            });

            return NamespaceDeclaration(namespaceName)
                .WithUsings(usingsDirectives)
                .WithMembers(declarations);
        }

        private static InvocationExpressionSyntax GetEnum(string toCall, IdentifierNameSyntax className)
        {
            MemberAccessExpressionSyntax member(string toCall)
               => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonElementParameterName, GenericName(toCall).AddTypeArgumentListArguments(className));

            return InvocationExpression(member(toCall)
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_propertyNameParameterName) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
        }

        private static InvocationExpressionSyntax WriteEnum(string toCall, IdentifierNameSyntax className)
        {
            return InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonWriterParameterName, IdentifierName(toCall))
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_propertyNameParameterName), Argument(_valueParameterName) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
        }

        private static InvocationExpressionSyntax GetEnumValue(string toCall, IdentifierNameSyntax className)
        {
            MemberAccessExpressionSyntax member(string toCall)
               => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonElementParameterName, GenericName(toCall).AddTypeArgumentListArguments(className));

            return InvocationExpression(member(toCall)
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList()
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
        }

        private static IEnumerable<MethodDeclarationSyntax> JsonGetEnumMethods(this MetaType sourceMetaType, string methodToCall, IdentifierNameSyntax identifierSyntax)
        {
            var getMethodName = Identifier($"Get{sourceMetaType.ClassNameIdentifier.Text}");
            var tryGetMethodName = Identifier($"TryGet{sourceMetaType.ClassNameIdentifier.Text}");
            var classNameSyntax = sourceMetaType.ClassName;

            yield return MethodDeclaration(identifierSyntax, getMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter })))
                .WithExpressionBody(ArrowExpressionClause(GetEnumValue(methodToCall, classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(identifierSyntax, getMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(GetEnum(methodToCall, classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(SyntaxFactory.NullableType(identifierSyntax), tryGetMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(GetEnum($"Try{methodToCall}", classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        }

        private static IEnumerable<MethodDeclarationSyntax> JsonWriteEnumMethods(this MetaType sourceMetaType, string methodToCall, IdentifierNameSyntax identifierSyntax)
        {
            var writeMethodName = Identifier($"Write{sourceMetaType.ClassNameIdentifier.Text}");
            var safeWriteMethodName = Identifier($"SafeWrite{sourceMetaType.ClassNameIdentifier.Text}");
            var classNameSyntax = sourceMetaType.ClassName;
            var valueParameter = Parameter(_valueParameterName.Identifier).WithType(identifierSyntax);
            var nullableValueParameter = Parameter(_valueParameterName.Identifier).WithType(SyntaxFactory.NullableType(identifierSyntax));


            yield return MethodDeclaration(_voidTypeSyntax, writeMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, valueParameter })))
                .WithExpressionBody(ArrowExpressionClause(WriteEnum(methodToCall, classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(_voidTypeSyntax, safeWriteMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, nullableValueParameter })))
                .WithExpressionBody(ArrowExpressionClause(WriteEnum($"Safe{methodToCall}", classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        }

    }
}
