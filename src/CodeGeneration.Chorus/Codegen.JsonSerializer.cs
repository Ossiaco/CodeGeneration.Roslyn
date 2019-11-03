namespace CodeGeneration.Chorus
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
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

        private static async Task<MemberDeclarationSyntax> CreateJsonSerializerForinterfaceAsync(MetaType sourceMetaType, NameSyntax namespaceSyntax)
        {

            var mergedFeatures = new List<FeatureGenerator>();
            mergedFeatures.Merge(new StyleCopCompliance(sourceMetaType));

            var className = Identifier($"{sourceMetaType.ClassNameIdentifier.Text}JsonSerializer");
            var namespaceName = ParseName(typeof(System.Text.Json.JsonElement).Namespace);

            var innerMembers = new List<MemberDeclarationSyntax>();
            innerMembers.AddRange(await CreateWriteValueMethodsAsync(sourceMetaType));

            var partialClass = ClassDeclaration(className)
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

        private static MethodDeclarationSyntax IBufferWriterMethod(this MetaType sourceMetaType, string className, IdentifierNameSyntax interfaceType)
        {
            var methodName = Identifier($"Write{className}Value");
            var valueParameter = Parameter(_valueParameterName.Identifier).WithType(interfaceType);

            var body = new List<ExpressionStatementSyntax>();

            return MethodDeclaration(_intTypeSyntax, methodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _bufferWriterParameter, valueParameter })))
                .WithBody(Block(body));
        }

        private static async Task<IEnumerable<MethodDeclarationSyntax>> Utf8JsonWriterValueMethodsAsync(this MetaType sourceMetaType, string className, IdentifierNameSyntax interfaceType)
        {
            var methodName = $"Write{className}JsonValue";
            var methodNameIdentifier = Identifier(methodName);
            var valueParameter = Parameter(_valueParameterName.Identifier).WithType(interfaceType);

            static InvocationExpressionSyntax WriteJsonValue(MetaProperty metaProperty)
            {
                var nullable = metaProperty.IsNullable ? "Safe" : "";
                var array = metaProperty.IsCollection ? "Array" : "";
                var typeName = metaProperty.TypeClassName;
                return InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonWriterParameterName, IdentifierName($"{nullable}Write{typeName}{array}"))
                           .WithOperatorToken(Token(SyntaxKind.DotToken)))
                           .WithArgumentList(
                               ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken,
                                        Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(TriviaList(), metaProperty.NameAsJsonProperty, string.Empty, TriviaList()))),
                                        Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _valueParameterName, IdentifierName(metaProperty.Symbol.Name)))
                                       )
                                   )
                               .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                               .WithCloseParenToken(Token(SyntaxKind.CloseParenToken))
                               );
            }

            bool IsRequired(MetaProperty prop)
            {
                return prop.IsIgnored || sourceMetaType.AbstractJsonProperty == prop.Name ? false : true;
            }

            var properties = await sourceMetaType.GetLocalPropertiesAsync();
            var body = new List<ExpressionStatementSyntax>();

            if (await sourceMetaType.HasAncestorAsync())
            {
                var ancestor = await sourceMetaType.GetDirectAncestorAsync();
                var callAncestor = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonWriterParameterName, IdentifierName($"Write{ancestor.ClassName}JsonValue"))
                           .WithOperatorToken(Token(SyntaxKind.DotToken)))
                           .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(_valueParameterName)))
                               .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                               .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
                body.Add(ExpressionStatement(callAncestor));
            }

            body.AddRange(properties.Where(IsRequired).Select(f => ExpressionStatement(WriteJsonValue(f))));

            var definiteWrite = MethodDeclaration(_voidTypeSyntax, methodNameIdentifier)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, valueParameter })))
                .WithBody(Block(body));

            methodNameIdentifier = Identifier($"Write{className}Value");

            InvocationExpressionSyntax writeValueSyntax(string methodName) => InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonWriterParameterName, IdentifierName(methodName))
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_valueParameterName) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            var polyWrite = MethodDeclaration(_voidTypeSyntax, methodNameIdentifier)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, valueParameter })))
                .WithExpressionBody(ArrowExpressionClause(writeValueSyntax(methodName)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            return new[] { definiteWrite, polyWrite };
        }

        private static IEnumerable<MethodDeclarationSyntax> Utf8JsonWriterPropertyMethods(this MetaType sourceMetaType, string className, IdentifierNameSyntax interfaceType)
        {
            var methodName = Identifier($"Write{className}");
            var safeMethodName = Identifier($"SafeWrite{className}");
            var valueParameter = Parameter(_valueParameterName.Identifier).WithType(interfaceType);
            var nullableValueParameter = Parameter(_valueParameterName.Identifier).WithType(SyntaxFactory.NullableType(interfaceType));
            var callbackMethodName = IdentifierName($"Write{className}Value");

            InvocationExpressionSyntax writeObjectValue(string methodName) => InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonWriterParameterName, IdentifierName(methodName))
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_propertyNameParameterName), Argument(_valueParameterName), Argument(callbackMethodName) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            yield return MethodDeclaration(_voidTypeSyntax, methodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, valueParameter })))
                .WithExpressionBody(ArrowExpressionClause(writeObjectValue("WriteObject")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(_voidTypeSyntax, safeMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, nullableValueParameter })))
                .WithExpressionBody(ArrowExpressionClause(writeObjectValue("SafeWriteObject")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        private static IEnumerable<MethodDeclarationSyntax> Utf8JsonWriterArrayMethods(this MetaType sourceMetaType, string className, IdentifierNameSyntax interfaceType)
        {
            var methodName = Identifier($"Write{className}Array");
            var safeMethodName = Identifier($"SafeWrite{className}Array");
            var interfaceEnumerableType = GenericName(Identifier("IEnumerable"), TypeArgumentList(SingletonSeparatedList((TypeSyntax)interfaceType)));
            var valueParameter = Parameter(_valueParameterName.Identifier).WithType(interfaceEnumerableType);
            var nullableValueParameter = Parameter(_valueParameterName.Identifier).WithType(SyntaxFactory.NullableType(interfaceEnumerableType));
            var callbackMethodName = IdentifierName($"Write{className}Value");
            var valueParameterName = IdentifierName("value");
            var writeMethodName = IdentifierName("WriteArray");
            var safeWriteMethodName = IdentifierName("SafeWriteArray");

            var arrowArraySyntax = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonWriterParameterName, writeMethodName)
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(valueParameterName), Argument(callbackMethodName) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            yield return MethodDeclaration(_voidTypeSyntax, methodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, valueParameter })))
                .WithExpressionBody(ArrowExpressionClause(arrowArraySyntax))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            InvocationExpressionSyntax getArraySyntax(IdentifierNameSyntax method) => InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonWriterParameterName, method)
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_propertyNameParameterName), Argument(valueParameterName), Argument(callbackMethodName) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            yield return MethodDeclaration(_voidTypeSyntax, methodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, valueParameter })))
                .WithExpressionBody(ArrowExpressionClause(getArraySyntax(writeMethodName)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(_voidTypeSyntax, safeMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, nullableValueParameter })))
                .WithExpressionBody(ArrowExpressionClause(getArraySyntax(safeWriteMethodName)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        }

        private static IEnumerable<MethodDeclarationSyntax> JsonGetMethods(this MetaType sourceMetaType, string className, IdentifierNameSyntax interfaceType)
        {
            var getMethodName = Identifier($"Get{className}");
            var tryGetMethodName = Identifier($"TryGet{className}");
            var methodNameParameter = IdentifierName($"Get{className}");
            var classNameSyntax = sourceMetaType.ClassName;

            var body = Block();

            var newObjectExpression = ObjectCreationExpression(classNameSyntax, ArgumentList(SingletonSeparatedList(Argument(_jsonElementParameterName))), null);
            yield return MethodDeclaration(interfaceType, getMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter })))
                .WithExpressionBody(ArrowExpressionClause(newObjectExpression))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            InvocationExpressionSyntax callGetObject(string toCall) => InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonElementParameterName, IdentifierName(toCall))
               .WithOperatorToken(Token(SyntaxKind.DotToken)))
               .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_propertyNameParameterName), Argument(methodNameParameter) }))
                   .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                   .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            yield return MethodDeclaration(interfaceType, getMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(callGetObject("GetObject")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            yield return MethodDeclaration(SyntaxFactory.NullableType(interfaceType), tryGetMethodName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter, _propertyNameParameter })))
                .WithExpressionBody(ArrowExpressionClause(callGetObject("TryGetObject")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        }

        private static async Task<IEnumerable<MethodDeclarationSyntax>> CreateWriteValueMethodsAsync(MetaType sourceMetaType)
        {
            var className = sourceMetaType.ClassNameIdentifier.Text;
            var interfaceType = IdentifierName(sourceMetaType.InterfaceNameIdentifier);

            var result = new List<MethodDeclarationSyntax>();

            //result.Add(sourceMetaType.IBufferWriterMethod(className, interfaceType));
            result.AddRange(await sourceMetaType.Utf8JsonWriterValueMethodsAsync(className, interfaceType));
            result.AddRange(sourceMetaType.Utf8JsonWriterPropertyMethods(className, interfaceType));
            result.AddRange(sourceMetaType.Utf8JsonWriterArrayMethods(className, interfaceType));
            result.AddRange(sourceMetaType.JsonGetMethods(className, interfaceType));


            return result;
        }

        private static IEnumerable<StatementSyntax> WriteValueStatements()
        {
            // yield return SyntaxFactory.
            yield break;
        }
    }
}
