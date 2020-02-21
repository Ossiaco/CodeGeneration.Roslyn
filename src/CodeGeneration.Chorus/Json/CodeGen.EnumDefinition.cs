using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeGeneration.Chorus.Json
{
    internal partial class CodeGen
    {
        private MemberDeclarationSyntax CreateEnumSerializerClass()
        {

            var namespaceSyntax = metaType.DeclarationSyntax.Ancestors().OfType<NamespaceDeclarationSyntax>().Single().Name.WithoutTrivia();
            var identifierSyntax = IdentifierName(metaType.InterfaceNameIdentifier);
            var enumerableSyntax = IdentifierName($"IEnumerable<{metaType.InterfaceNameIdentifier.ValueText}>");

            var className = $"{metaType.ClassNameIdentifier.Text}JsonSerializer";
            var namespaceName = ParseName(typeof(System.Text.Json.JsonElement).Namespace);

            var innerMembers = new List<MemberDeclarationSyntax>();

            if (metaType.IsEnumAsString)
            {
                innerMembers.AddRange(JsonGetEnumMethods("GetEnumString", identifierSyntax));
                innerMembers.AddRange(JsonWriteEnumMethods("WriteEnumString", identifierSyntax, enumerableSyntax));
            }
            else
            {
                innerMembers.AddRange(JsonGetEnumMethods("GetEnum", identifierSyntax));
                innerMembers.AddRange(JsonWriteEnumMethods("WriteEnum", identifierSyntax, enumerableSyntax));
            }

            var partialClass = ClassDeclaration(Identifier(className))
                .WithModifiers(metaType.DeclarationSyntax.Modifiers)
                .AddModifiers(Token(SyntaxKind.StaticKeyword))
                .WithMembers(List(innerMembers));

            var declarations = List<MemberDeclarationSyntax>();
            declarations = declarations.Add(partialClass);
            var usingsDirectives = List(new[] {
                UsingDirective(ParseName(typeof(System.Buffers.IBufferWriter<byte>).Namespace)),
                UsingDirective(ParseName("Chorus.Text.Json")),
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
               => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _elementName, GenericName(toCall).AddTypeArgumentListArguments(className));

            return InvocationExpression(member(toCall)
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_propertyNameName) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
        }

        private static InvocationExpressionSyntax WriteEnum(string toCall, IdentifierNameSyntax className)
        {
            return InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _writerName, IdentifierName(toCall))
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_propertyNameName), Argument(_valueName) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
        }

        private static InvocationExpressionSyntax GetEnumValue(string toCall, IdentifierNameSyntax className)
        {
            MemberAccessExpressionSyntax member(string toCall)
               => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _elementName, GenericName(toCall).AddTypeArgumentListArguments(className));

            return InvocationExpression(member(toCall)
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList()
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
        }

        private IEnumerable<MethodDeclarationSyntax> JsonGetEnumMethods(string methodToCall, IdentifierNameSyntax enumTypeSyntax)
        {
            var typeName = metaType.ClassNameIdentifier.Text;
            var getMethodName = Identifier($"Get{typeName}");
            var getArrayMethodName = Identifier($"Get{typeName}Array");
            var tryGetMethodName = Identifier($"TryGet{typeName}");
            var tryGetArrayMethodName = Identifier($"TryGet{typeName}Array");
            var classNameSyntax = metaType.ClassName;
            var methodNameParameter = IdentifierName($"Get{typeName}");

            var interfaceArrayType = ArrayType(enumTypeSyntax)
                .AddRankSpecifiers(ArrayRankSpecifier()
                .AddSizes(OmittedArraySizeExpression()));

            yield return MethodDeclaration(enumTypeSyntax, getMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter })))
                .WithExpressionBody(ArrowExpressionClause(GetEnumValue(methodToCall, classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(enumTypeSyntax, getMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(GetEnum(methodToCall, classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(NullableType(enumTypeSyntax), tryGetMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(GetEnum($"Try{methodToCall}", classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(interfaceArrayType, getArrayMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter })))
                .WithExpressionBody(ArrowExpressionClause(GetEnumValue($"{methodToCall}Array", classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(interfaceArrayType, getArrayMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(GetEnum($"{methodToCall}Array", classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(SyntaxFactory.NullableType(interfaceArrayType), tryGetArrayMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(GetEnum($"Try{methodToCall}Array", classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        private IEnumerable<MethodDeclarationSyntax> JsonWriteEnumMethods(string methodToCall, IdentifierNameSyntax identifierSyntax, IdentifierNameSyntax enumerableSyntax)
        {
            var writeArrayMethodName = Identifier($"Write{metaType.ClassNameIdentifier.Text}Array");
            var safeWriteMethodName = Identifier($"SafeWrite{metaType.ClassNameIdentifier.Text}");
            var writeMethodName = Identifier($"Write{metaType.ClassNameIdentifier.Text}");
            var safeWriteArrayMethodName = Identifier($"SafeWrite{metaType.ClassNameIdentifier.Text}Array");
            var classNameSyntax = metaType.ClassName;
            var valueParameter = Parameter(_valueName.Identifier).WithType(identifierSyntax);
            var nullableValueParameter = Parameter(_valueName.Identifier).WithType(NullableType(identifierSyntax));
            var arrayParameter = Parameter(_valueName.Identifier).WithType(enumerableSyntax);
            var nullableArrayParameter = Parameter(_valueName.Identifier).WithType(NullableType(enumerableSyntax));

            yield return MethodDeclaration(_voidType, writeMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, valueParameter })))
                .WithExpressionBody(ArrowExpressionClause(WriteEnum(methodToCall, classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(_voidType, safeWriteMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, nullableValueParameter })))
                .WithExpressionBody(ArrowExpressionClause(WriteEnum($"Safe{methodToCall}", classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(_voidType, writeArrayMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, arrayParameter })))
                .WithExpressionBody(ArrowExpressionClause(WriteEnum($"{methodToCall}Array", classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(_voidType, safeWriteArrayMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, nullableArrayParameter })))
                .WithExpressionBody(ArrowExpressionClause(WriteEnum($"Safe{methodToCall}Array", classNameSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        }

    }
}
