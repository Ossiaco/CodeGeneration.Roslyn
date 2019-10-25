namespace Chorus.CodeGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public partial class CodeGen
    {
        private static readonly IdentifierNameSyntax _jsonBufferWriterParameterName = SyntaxFactory.IdentifierName("buffer");
        private static readonly IdentifierNameSyntax _jsonWriterParameterName = SyntaxFactory.IdentifierName("writer");
        private static readonly IdentifierNameSyntax _propertyNameParameterName = SyntaxFactory.IdentifierName("propertyName");
        private static readonly IdentifierNameSyntax _jsonElementParameterName = SyntaxFactory.IdentifierName("element");
        private static readonly IdentifierNameSyntax _valueParameterName = SyntaxFactory.IdentifierName("value");
        private static readonly TypeSyntax _voidTypeSyntax = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
        private static readonly TypeSyntax _byteTypeSyntax = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ByteKeyword));
        private static readonly TypeSyntax _intTypeSyntax = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));
        private static readonly TypeSyntax _stringTypeSyntax = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword));
        private static readonly TypeSyntax _bufferWriterType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("IBufferWriter"), SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(_byteTypeSyntax)));
        private static readonly TypeSyntax _utf8JsonWriterType = SyntaxFactory.ParseName(nameof(System.Text.Json.Utf8JsonWriter));
        private static readonly ParameterSyntax _utf8JsonWriterThisParameter = SyntaxFactory.Parameter(_jsonWriterParameterName.Identifier).WithType(_utf8JsonWriterType).AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));
        private static readonly ParameterSyntax _jsonElementThisParameter = SyntaxFactory.Parameter(_jsonElementParameterName.Identifier).WithType(SyntaxFactory.ParseName(nameof(System.Text.Json.JsonElement))).AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));
        private static readonly ParameterSyntax _bufferWriterParameter = SyntaxFactory.Parameter(_jsonBufferWriterParameterName.Identifier).WithType(_bufferWriterType).AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));
        private static readonly ParameterSyntax _propertyNameParameter = SyntaxFactory.Parameter(_propertyNameParameterName.Identifier).WithType(_stringTypeSyntax);
        private MemberDeclarationSyntax CreateJsonSerializer(ClassDeclarationSyntax applyTo, NameSyntax namespaceSyntax)
        {

            var mergedFeatures = new List<FeatureGenerator>();
            mergedFeatures.Merge(new StyleCopCompliance(this));
            mergedFeatures.Merge(new NullableType(this));

            var className = SyntaxFactory.Identifier($"{_applyToTypeName.Identifier.Text}JsonSerializer");
            var namespaceName = SyntaxFactory.ParseName(typeof(System.Text.Json.JsonElement).Namespace);

            var innerMembers = new List<MemberDeclarationSyntax>();
            innerMembers.AddRange(CreateWriteValueMethods(applyTo, namespaceSyntax));

            var partialClass = SyntaxFactory.ClassDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .WithMembers(SyntaxFactory.List(innerMembers));

            partialClass = mergedFeatures.
                Aggregate(partialClass, (acc, feature) => feature.ProcessApplyToClassDeclaration(acc));

            var declarations = SyntaxFactory.List<MemberDeclarationSyntax>();
            declarations = declarations.Add(partialClass);

            var usingsDirectives = SyntaxFactory.List(new[] {
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(System.Buffers.IBufferWriter<byte>).Namespace)),
                SyntaxFactory.UsingDirective(namespaceSyntax),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(IEnumerable<string>).Namespace))
            });

            return SyntaxFactory.NamespaceDeclaration(namespaceName)
                .WithUsings(usingsDirectives)
                .WithMembers(declarations);
        }

        private IEnumerable<MethodDeclarationSyntax> CreateWriteValueMethods(ClassDeclarationSyntax applyTo, NameSyntax namespaceSyntax)
        {
            var className = _applyToTypeName.Identifier.Text;
            var methodName = SyntaxFactory.Identifier($"Write{className}Value");
            var interfaceType = SyntaxFactory.IdentifierName(_interfaceDeclaration.Identifier);
            var classType = SyntaxFactory.IdentifierName(applyTo.Identifier);
            var interfaceEnumerableType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("IEnumerable"), SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList((TypeSyntax)interfaceType)));

            var body = SyntaxFactory.Block();
            var valueParameter = SyntaxFactory.Parameter(_valueParameterName.Identifier).WithType(interfaceType);

            yield return SyntaxFactory.MethodDeclaration(_intTypeSyntax, methodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .WithParameterList(SyntaxFactory.ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _bufferWriterParameter, valueParameter })))
                .WithBody(body);


            yield return SyntaxFactory.MethodDeclaration(_voidTypeSyntax, methodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .WithParameterList(SyntaxFactory.ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, valueParameter })))
                .WithBody(body);

            methodName = SyntaxFactory.Identifier($"Write{className}");
            valueParameter = SyntaxFactory.Parameter(_valueParameterName.Identifier).WithType(SyntaxFactory.NullableType(interfaceType));

            yield return SyntaxFactory.MethodDeclaration(_voidTypeSyntax, methodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .WithParameterList(SyntaxFactory.ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, valueParameter })))
                .WithBody(body);

            methodName = SyntaxFactory.Identifier($"Write{className}Array");
            valueParameter = SyntaxFactory.Parameter(_valueParameterName.Identifier).WithType(interfaceEnumerableType);

            yield return SyntaxFactory.MethodDeclaration(_voidTypeSyntax, methodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .WithParameterList(SyntaxFactory.ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, valueParameter })))
                .WithBody(body);

            valueParameter = SyntaxFactory.Parameter(_valueParameterName.Identifier).WithType(SyntaxFactory.NullableType(interfaceEnumerableType));

            yield return SyntaxFactory.MethodDeclaration(_voidTypeSyntax, methodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .WithParameterList(SyntaxFactory.ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterThisParameter, _propertyNameParameter, valueParameter })))
                .WithBody(body);

            methodName = SyntaxFactory.Identifier($"Get{className}");
            yield return SyntaxFactory.MethodDeclaration(interfaceType, methodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .WithParameterList(SyntaxFactory.ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementThisParameter })))
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ObjectCreationExpression(classType, SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(_jsonElementParameterName))), null)))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

            // yield return SyntaxFactory.MethodDeclaration(_voidTypeSyntax, methodName)
            //    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            //    .WithParameterList(SyntaxFactory.ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _jsonElementParameter, _propertyNameParameter, valueParameter })))
            //    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ObjectCreationExpression(_applyToMetaType.TypeSyntax, SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(_jsonElementParameterName))), null)));


        }

        private IEnumerable<StatementSyntax> WriteValueStatements()
        {
            // yield return SyntaxFactory.
            yield return default;
        }
    }
}
