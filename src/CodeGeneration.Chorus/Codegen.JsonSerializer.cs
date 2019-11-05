namespace CodeGeneration.Chorus
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

    internal partial class CodeGen
    {
        private static readonly IdentifierNameSyntax _jsonBufferWriterParameterName = IdentifierName("buffer");
        private static readonly IdentifierNameSyntax _jsonWriterParameterName = IdentifierName("writer");
        private static readonly IdentifierNameSyntax _propertyNameParameterName = IdentifierName("propertyName");
        private static readonly IdentifierNameSyntax _jsonElementParameterName = IdentifierName("element");
        private static readonly IdentifierNameSyntax _valueParameterName = IdentifierName("value");
        private static readonly TypeSyntax _voidTypeSyntax = PredefinedType(Token(SyntaxKind.VoidKeyword));
        private static readonly TypeSyntax _byteTypeSyntax = PredefinedType(Token(SyntaxKind.ByteKeyword));
        private static readonly TypeSyntax _intTypeSyntax = PredefinedType(Token(SyntaxKind.IntKeyword));
        private static readonly TypeSyntax _stringTypeSyntax = PredefinedType(Token(SyntaxKind.StringKeyword));
        private static readonly TypeSyntax _bufferWriterType = GenericName(Identifier("IBufferWriter"), TypeArgumentList(SingletonSeparatedList(_byteTypeSyntax)));
        private static readonly TypeSyntax _utf8JsonWriterType = ParseName(nameof(System.Text.Json.Utf8JsonWriter));
        private static readonly ParameterSyntax _utf8JsonWriterParameter = Parameter(_jsonWriterParameterName.Identifier).WithType(_utf8JsonWriterType);
        private static readonly ParameterSyntax _utf8JsonWriterThisParameter = _utf8JsonWriterParameter.AddModifiers(Token(SyntaxKind.ThisKeyword));
        private static readonly ParameterSyntax _jsonElementThisParameter = Parameter(_jsonElementParameterName.Identifier).WithType(ParseName(nameof(System.Text.Json.JsonElement))).AddModifiers(Token(SyntaxKind.ThisKeyword));
        private static readonly ParameterSyntax _bufferWriterParameter = Parameter(_jsonBufferWriterParameterName.Identifier).WithType(_bufferWriterType).AddModifiers(Token(SyntaxKind.ThisKeyword));
        private static readonly ParameterSyntax _propertyNameParameter = Parameter(_propertyNameParameterName.Identifier).WithType(_stringTypeSyntax);

        private static async Task<MemberDeclarationSyntax> CreateJsonSerializerForinterfaceAsync(MetaType sourceMetaType)
        {

            var className = Identifier($"{sourceMetaType.ClassNameIdentifier.Text}JsonSerializer");
            var namespaceName = ParseName(typeof(System.Text.Json.JsonElement).Namespace);

            var innerMembers = new List<MemberDeclarationSyntax>();
            innerMembers.AddRange(await StaticGetValueMethodsAsync(sourceMetaType));

            var partialClass = ClassDeclaration(className)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithMembers(List(innerMembers));

            var declarations = List<MemberDeclarationSyntax>();
            declarations = declarations.Add(partialClass);
            var usingsDirectives = List(new[] {
                UsingDirective(ParseName(typeof(System.Buffers.IBufferWriter<byte>).Namespace)),
                UsingDirective(ParseName("Chorus.Common.Text.Json")),
                UsingDirective(sourceMetaType.Namespace),
                UsingDirective(ParseName(typeof(IEnumerable<string>).Namespace))
            });

            return NamespaceDeclaration(namespaceName)
                .WithUsings(usingsDirectives)
                .WithMembers(declarations);
        }

        private static async Task<IEnumerable<MethodDeclarationSyntax>> JsonGetMethodsAsync(this MetaType sourceMetaType, string className, IdentifierNameSyntax interfaceType)
        {
            var getArrayMethodName = Identifier($"Get{className}Array");
            var getMethodName = Identifier($"Get{className}");
            var tryGetMethodName = Identifier($"TryGet{className}");
            var tryGetArrayMethodName = Identifier($"TryGet{className}Array");
            var methodNameParameter = IdentifierName($"Get{className}");
            var classNameSyntax = sourceMetaType.ClassName;

            var interfaceArrayType = ArrayType(interfaceType)
                    .AddRankSpecifiers(ArrayRankSpecifier()
                    .AddSizes(OmittedArraySizeExpression()));

            var result = new List<MethodDeclarationSyntax>();
            var newObjectExpression = ObjectCreationExpression(classNameSyntax, ArgumentList(SingletonSeparatedList(Argument(_jsonElementParameterName))), null);

            if (sourceMetaType.IsAbstractType)
            {
                result.Add(await AbstractGetObjectAsync(sourceMetaType, interfaceType, getMethodName));
            }
            else
            {
                result.Add(MethodDeclaration(interfaceType, getMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter })))
                .WithExpressionBody(ArrowExpressionClause(newObjectExpression))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
            }

            InvocationExpressionSyntax callGetObject(string toCall) => InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonElementParameterName, IdentifierName(toCall))
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_propertyNameParameterName), Argument(methodNameParameter) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            result.Add(MethodDeclaration(interfaceType, getMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(callGetObject("GetObject")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            result.Add(MethodDeclaration(SyntaxFactory.NullableType(interfaceType), tryGetMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(callGetObject("TryGetObject")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            result.Add(MethodDeclaration(interfaceArrayType, getArrayMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(callGetObject("GetObjectArray")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            result.Add(MethodDeclaration(SyntaxFactory.NullableType(interfaceArrayType), tryGetArrayMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(callGetObject("TryGetObjectArray")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            return result;
        }

        public static ExpressionSyntax FormatValue(this TypedConstant constant)
        {
            //TODO: add hex formatting
            switch (constant.Type)
            {
                default:
                    return (ExpressionSyntax)Syntax.Generator.TypedConstantExpression(constant);
            }
        }
        private static SwitchExpressionArmSyntax GetArm(MetaType descendent, INamedTypeSymbol abstractAtrribute)
        {
            var constValue = descendent.GetAbstractJsonAttributeValue(abstractAtrribute).FormatValue();
            var newObjectExpression = ObjectCreationExpression(descendent.FullyQualifiedClassName, ArgumentList(SingletonSeparatedList(Argument(_jsonElementParameterName))), null);
            return SwitchExpressionArm(ConstantPattern(constValue), newObjectExpression);
        }


        private static async Task<MethodDeclarationSyntax> AbstractGetObjectAsync(MetaType sourceMetaType, IdentifierNameSyntax interfaceType, SyntaxToken getMethodName)
        {
            var paramName = IdentifierName("json");
            var abstractMetaType = sourceMetaType;
            while (!abstractMetaType.HasAbstractJsonProperty)
            {
                abstractMetaType = await abstractMetaType.GetDirectAncestorAsync();
            }

            var abstractProperty = (await abstractMetaType.GetLocalPropertiesAsync()).Single(p => p.Name == abstractMetaType.AbstractJsonProperty);
            var thisExpresion = Syntax.ThisDot(abstractProperty.NameAsProperty);

            var getJsonValue = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonElementParameterName, abstractProperty.GetJsonValueName())
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
                       .WithArgumentList(
                           ArgumentList(SingletonSeparatedList(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(TriviaList(), abstractProperty.NameAsJsonProperty, string.Empty, TriviaList())))))
                               .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                               .WithCloseParenToken(Token(SyntaxKind.CloseParenToken))
                           );

            var defaultThrow = SwitchExpressionArm(DiscardPattern(),
                ThrowExpression(ObjectCreationExpression(Syntax.GetTypeSyntax(typeof(System.Text.Json.JsonException))).AddArgumentListArguments(
                    Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"Invalid {abstractMetaType.AbstractJsonProperty} specified."))))));

            var allDescendents = await sourceMetaType.GetResursiveDescendentsAsync();
            var arms = allDescendents.Select(t => GetArm(t, abstractMetaType.AbstractAttribute)).ToList();
            arms.Add(defaultThrow);

            return MethodDeclaration(interfaceType, getMethodName)
                   .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                   .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter })))
                   .WithExpressionBody(ArrowExpressionClause(SwitchExpression(getJsonValue, SeparatedList<SwitchExpressionArmSyntax>().AddRange(arms))))
                   .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        private static async Task<IEnumerable<MethodDeclarationSyntax>> StaticGetValueMethodsAsync(MetaType sourceMetaType)
        {
            var className = sourceMetaType.ClassNameIdentifier.Text;
            var interfaceType = IdentifierName(sourceMetaType.InterfaceNameIdentifier);

            var result = new List<MethodDeclarationSyntax>();
            result.AddRange(await sourceMetaType.JsonGetMethodsAsync(className, interfaceType));
            return result;
        }

    }
}
