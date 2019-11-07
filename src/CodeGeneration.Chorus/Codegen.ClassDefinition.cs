//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

    internal partial class CodeGen
    {
        private static BaseExpressionSyntax _baseExpression = BaseExpression(SyntaxFactory.Token(SyntaxKind.BaseKeyword));

        public static FieldDeclarationSyntax CreateConstMember(TypedConstant constant, string name)
        {
            if (constant.Kind != TypedConstantKind.Error)
            {
                var syntax = constant.Type.GetFullyQualifiedSymbolName(NullableAnnotation.None);
                var value = Syntax.Generator.TypedConstantExpression(constant);
                if (value is ExpressionSyntax expr)
                {
                    return FieldDeclaration(
                    VariableDeclaration(syntax)
                        .AddVariables(
                            VariableDeclarator(Identifier(name.ToUpperInvariant()))
                            .WithInitializer(
                                EqualsValueClause(expr))))
                    .AddModifiers(
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.ConstKeyword));
                }
            }
            return null;
        }

        private static async Task<MemberDeclarationSyntax> CreateClassDeclarationAsync(MetaType sourceMetaType)
        {
            var innerMembers = new List<MemberDeclarationSyntax>();
            var hasAncestor = await sourceMetaType.HasAncestorAsync();

            PropertyDeclarationSyntax CreatePropertyDeclaration(MetaProperty prop)
            {
                var result = prop.PropertyDeclarationSyntax;
                if (prop.Name == sourceMetaType.AbstractJsonProperty)
                {
                    result = result.AddModifiers(Token(SyntaxKind.AbstractKeyword));
                }
                return result;
            }

            var abstractImplementations = await sourceMetaType.GetPropertyOverridesAsync();
            var isAbstract = sourceMetaType.IsAbstractType;
            var localProperties = await sourceMetaType.GetLocalPropertiesAsync();

            innerMembers.AddRange(await sourceMetaType.CreateJsonCtorAsync(hasAncestor));
            if (sourceMetaType.IsAssignableFrom(_responseMessageType))
            {
                innerMembers.AddRange(await CreateResponseCtorAsync(sourceMetaType));
            }
            else
            {
                innerMembers.AddRange(await CreatePublicCtorAsync(sourceMetaType, hasAncestor));
            }
            innerMembers.AddRange(abstractImplementations);
            innerMembers.AddRange(localProperties.Select(CreatePropertyDeclaration));
            if (localProperties.Count > 0)
            {
                innerMembers.Add(await ToJsonMethodAsync(sourceMetaType));
            }

            var partialClass = ClassDeclaration(sourceMetaType.ClassNameIdentifier)
                 .AddBaseListTypes(sourceMetaType.SemanticModel.AsFullyQualifiedBaseType((TypeDeclarationSyntax)sourceMetaType.DeclarationSyntax).ToArray())
                 .AddModifiers(Token(SyntaxKind.PublicKeyword))
                 .WithMembers(List(innerMembers));

            if (isAbstract)
            {
                partialClass = partialClass.AddModifiers(Token(SyntaxKind.AbstractKeyword));
            }

            partialClass = partialClass.AddModifiers(Token(SyntaxKind.PartialKeyword));

            var outerMembers = List<MemberDeclarationSyntax>();
            outerMembers = outerMembers.Add(partialClass);

            var usingsDirectives = List(new[] {
                UsingDirective(ParseName(typeof(System.Text.Json.JsonElement).Namespace)),
                UsingDirective(ParseName("Chorus.Common.Text.Json")),
            });

            var ns = sourceMetaType.DeclarationSyntax.Ancestors().OfType<NamespaceDeclarationSyntax>().Single().Name.WithoutTrivia();
            var members = NamespaceDeclaration(ns)
                 .WithUsings(usingsDirectives)
                 .WithMembers(outerMembers);

            return members;
        }

        private static async Task<IEnumerable<MemberDeclarationSyntax>> CreateJsonCtorAsync(this MetaType sourceMetaType, bool hasAncestor)
        {
            var paramName = IdentifierName("json");
            var param = Parameter(paramName.Identifier).WithType(ParseName(nameof(System.Text.Json.JsonElement)));

            var body = new List<StatementSyntax>();

            if (sourceMetaType.HasAbstractJsonProperty)
            {
                var f = (await sourceMetaType.GetLocalPropertiesAsync()).Single(p => p.Name == sourceMetaType.AbstractJsonProperty);
                var thisExpresion = Syntax.ThisDot(f.NameAsProperty);

                var expression = IfStatement(
                    BinaryExpression(SyntaxKind.NotEqualsExpression, f.GetJsonValue(paramName), Syntax.ThisDot(f.NameAsProperty)),
                    ThrowStatement(
                        ObjectCreationExpression(Syntax.GetTypeSyntax(typeof(System.Text.Json.JsonException))).AddArgumentListArguments(
                            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"Invalid {sourceMetaType.AbstractJsonProperty} specified."))))));
                body.Add(expression);
            }

            bool IsRequired(MetaProperty prop)
            {
                return prop.IsIgnored || sourceMetaType.AbstractJsonProperty == prop.Name ? false : true;
            }

            body.AddRange(
                (await sourceMetaType.GetLocalPropertiesAsync()).Where(IsRequired).Select(f =>
                ExpressionStatement(
                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(f.NameAsProperty.Identifier), f.GetJsonValue(paramName))))
                );

            var ctor = ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                .AddModifiers(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.ProtectedKeyword));

            ctor = ctor.WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { param })))
               .WithBody(Block(body));

            if (hasAncestor)
            {
                ctor = ctor.WithInitializer(ConstructorInitializer(
                    SyntaxKind.BaseConstructorInitializer,
                    ArgumentList(SingletonSeparatedList(Argument(paramName)))));
            }

            return new[] { ctor };
        }

        private static async Task<IEnumerable<MemberDeclarationSyntax>> CreatePublicCtorAsync(MetaType sourceMetaType, bool hasAncestor)
        {
            var localProperties = (await sourceMetaType.GetLocalPropertiesAsync()).Where(p => p.isRequired);
            var body = Block(localProperties.Select(p => p.PropertyAssignment));

            var allProperties = (await sourceMetaType.GetAllPropertiesAsync()).Where(p => p.isRequired);
            var orderedArguments = allProperties.Where(p => !p.IsNullable).OrderBy(p => p.Name)
                    .Concat(allProperties.Where(p => p.IsNullable).OrderBy(p => p.Name));

            var ctor = ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                .AddModifiers(Token(sourceMetaType.HasAbstractJsonProperty ? SyntaxKind.ProtectedKeyword : SyntaxKind.PublicKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, CreateParameterList(orderedArguments))))
                .WithBody(body);

            if (hasAncestor)
            {
                var props = (await sourceMetaType.GetInheritedPropertiesAsync())
                    .Where(p => p.isRequired)
                    .OrderBy(p => p.Name)
                    .ToList();

                if (props.Count > 0)
                {
                    var arguments = CreateAssignmentList(props);
                    ctor = ctor.WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments));
                }
            }
            return new[] { ctor };
        }

        private static async Task<IEnumerable<MemberDeclarationSyntax>> CreateResponseCtorAsync(MetaType sourceMetaType)
        {
            var hasAncestor = !SymbolEqualityComparer.Default.Equals(_responseMessageType, sourceMetaType.TypeSymbol);
            var requestParam = IdentifierName("resquest");
            var requestParamType = Parameter(requestParam.Identifier).WithType(ParseName("Chorus.Common.Messaging.IRequestMessage"));

            var localProperties = (await sourceMetaType.GetLocalPropertiesAsync()).Where(p => p.isRequired);
            var body = Block(localProperties.Select(p => p.PropertyAssignment));

            static bool isRequired(MetaProperty p)
                => p.isRequired && !SymbolEqualityComparer.Default.Equals(p.MetaType.TypeSymbol, _responseMessageType);

            var allProperties = (await sourceMetaType.GetAllPropertiesAsync()).Where(isRequired);
            var orderedArguments = allProperties.Where(p => !p.IsNullable).OrderBy(p => p.Name)
                    .Concat(allProperties.Where(p => p.IsNullable).OrderBy(p => p.Name));

            var ctor = ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { requestParamType }.Concat(CreateParameterList(orderedArguments)))))
               .WithBody(body);

            if (hasAncestor)
            {
                var props = (await sourceMetaType.GetInheritedPropertiesAsync())
                    .Where(p => p.isRequired)
                    .OrderBy(p => p.Name)
                    .ToList();

                if (props.Count > 0)
                {
                    var arguments = CreateAssignmentList(props);
                    ctor = ctor.WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments));
                }
            }

            return new[] { ctor, await CreateResponseErrorCtorAsync(sourceMetaType, hasAncestor) };
        }

        private static async Task<MemberDeclarationSyntax> CreateResponseErrorCtorAsync(MetaType sourceMetaType, bool hasAncestor)
        {
            var requestParam = IdentifierName("resquest");
            var requestParamType = Parameter(requestParam.Identifier).WithType(ParseName("Chorus.Common.Messaging.IRequestMessage"));

            var exceptionParam = IdentifierName("error");
            var exceptionParamType = Parameter(exceptionParam.Identifier).WithType(ParseName(nameof(Exception)));

            var body = new List<StatementSyntax>();

            var ctor = ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { requestParamType, exceptionParamType })))
               .WithBody(Block(body));

            if (hasAncestor)
            {
                var arguments = CreateAssignmentList(new[] { requestParam, exceptionParam });
                ctor = ctor.WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments));
            }
            else
            {
                await Task.Delay(1);
            }

            return ctor;
        }

        private static async Task<MethodDeclarationSyntax> ToJsonMethodAsync(this MetaType sourceMetaType)
        {

            static InvocationExpressionSyntax WriteJsonValue(MetaProperty metaProperty)
            {
                var nullable = metaProperty.IsNullable ? "Safe" : "";
                var array = metaProperty.IsCollection ? "Array" : "";
                var typeName = metaProperty.JsonTypeName;
                return InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _jsonWriterParameterName, IdentifierName($"{nullable}Write{typeName}{array}"))
                           .WithOperatorToken(Token(SyntaxKind.DotToken)))
                           .WithArgumentList(
                               ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken,
                                        Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(TriviaList(), metaProperty.NameAsJsonProperty, string.Empty, TriviaList()))),
                                        Argument(Syntax.ThisDot(IdentifierName(metaProperty.Symbol.Name)))
                                       )
                                   )
                               .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                               .WithCloseParenToken(Token(SyntaxKind.CloseParenToken))
                               );
            }

            bool IsRequired(MetaProperty prop)
            {
                return !prop.IsIgnored;
            }

            var properties = await sourceMetaType.GetLocalPropertiesAsync();
            var body = new List<ExpressionStatementSyntax>();

            var hasAncestor = await sourceMetaType.HasAncestorAsync();
            if (hasAncestor)
            {
                var ancestor = await sourceMetaType.GetDirectAncestorAsync();
                var callAncestor = InvocationExpression(Syntax.BaseDot(IdentifierName($"ToJson"))
                           .WithOperatorToken(Token(SyntaxKind.DotToken)))
                           .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(_jsonWriterParameterName)))
                               .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                               .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
                body.Add(ExpressionStatement(callAncestor));
            }

            body.AddRange(properties.Where(IsRequired).Select(f => ExpressionStatement(WriteJsonValue(f))));

            var virtualOrOverride = hasAncestor ? Token(SyntaxKind.OverrideKeyword) : Token(SyntaxKind.VirtualKeyword);
            return MethodDeclaration(_voidTypeSyntax, Identifier("ToJson"))
                .AddModifiers(Token(SyntaxKind.PublicKeyword), virtualOrOverride)
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterParameter })))
                .WithBody(Block(body));
        }
    }
}
