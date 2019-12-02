//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus.Json
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
        private static readonly BaseExpressionSyntax _baseExpression = BaseExpression(Token(SyntaxKind.BaseKeyword));

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

        private async Task<MemberDeclarationSyntax> CreateClassDeclarationAsync(MetaType partialImplementation)
        {
            var innerMembers = new List<MemberDeclarationSyntax>();
            var hasAncestor = await this.metaType.HasAncestorAsync();

            PropertyDeclarationSyntax CreatePropertyDeclaration(MetaProperty prop)
            {
                var result = prop.PropertyDeclarationSyntax;
                if (prop.Name == this.metaType.AbstractJsonProperty)
                {
                    result = result.AddModifiers(Token(SyntaxKind.AbstractKeyword));
                }
                return result;
            }

            var abstractImplementations = await this.metaType.GetPropertyOverridesAsync();
            var isAbstract = this.metaType.IsAbstractType;
            var isSealed = partialImplementation?.TypeSymbol.IsSealed ?? false;
            var localProperties = await this.metaType.GetLocalPropertiesAsync();
            var directAncestor = await this.metaType.GetDirectAncestorAsync();

            innerMembers.AddRange(await JsonCtorAsync(hasAncestor, isSealed));

            if (context.ResponseMessageType != null && SymbolEqualityComparer.Default.Equals(context.ResponseMessageType, this.metaType.TypeSymbol))
            {
                innerMembers.AddRange(await ResponseMessageSyntax.AbstractResponseCtorAsync(this.metaType));
            }
            else if (context.ResponseMessageType != null && SymbolEqualityComparer.Default.Equals(context.ResponseMessageType, directAncestor.TypeSymbol))
            {
                innerMembers.AddRange(await ResponseCtorAsync());
            }
            else if (context.MessageType != null && SymbolEqualityComparer.Default.Equals(context.MessageType, this.metaType.TypeSymbol))
            {
                innerMembers.Add(MessageSyntax.AbstractMessageCtor(metaType));
            }
            else if (context.MessageType != null && this.metaType.IsAssignableFrom(context.MessageType))
            {
                innerMembers.Add(await MessageCtorAsync(directAncestor));
            }
            else
            {
                innerMembers.AddRange(await PublicCtorAsync(hasAncestor));
            }

            innerMembers.AddRange(abstractImplementations);
            innerMembers.AddRange(localProperties.Select(CreatePropertyDeclaration));
            if (localProperties.Count > 0)
            {
                innerMembers.Add(await ToJsonMethodAsync(isSealed));
            }
           
            var partialClass = ClassDeclaration(this.metaType.ClassNameIdentifier)
                 .AddBaseListTypes(this.metaType.SemanticModel.AsFullyQualifiedBaseType((TypeDeclarationSyntax)this.metaType.DeclarationSyntax, metaType.TransformationContext).ToArray())
                 .WithModifiers(this.metaType.DeclarationSyntax.Modifiers)
                 .WithMembers(List(innerMembers));

            if (isAbstract)
            {
                partialClass = partialClass.AddModifiers(Token(SyntaxKind.AbstractKeyword));
            }
            if (isSealed)
            {
                partialClass = partialClass.AddModifiers(Token(SyntaxKind.SealedKeyword));
            }
            partialClass = partialClass.AddModifiers(Token(SyntaxKind.PartialKeyword));

            var outerMembers = List<MemberDeclarationSyntax>();
            outerMembers = outerMembers.Add(partialClass);

            var usingsDirectives = List(new[] {
                UsingDirective(ParseName(typeof(System.Text.Json.JsonElement).Namespace)),
                UsingDirective(ParseName("Chorus.Text.Json")),
            });

            var ns = this.metaType.DeclarationSyntax
                .Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .Single()
                .Name
                .WithoutTrivia();

            var members = NamespaceDeclaration(ns)
                 .WithUsings(usingsDirectives)
                 .WithMembers(outerMembers);

            return members;

        }

        private async Task<IEnumerable<MemberDeclarationSyntax>> JsonCtorAsync(bool hasAncestor, bool isSealed)
        {
            bool IsRequired(MetaProperty prop) => prop.IsIgnored || this.metaType.AbstractJsonProperty == prop.Name ? false : true;

            var paramName = IdentifierName("json");
            var param = Parameter(paramName.Identifier).WithType(ParseName(nameof(System.Text.Json.JsonElement)));

            var body = new List<StatementSyntax>();

            if (this.metaType.HasAbstractJsonProperty)
            {
                var f = (await this.metaType.GetLocalPropertiesAsync()).Single(p => p.Name == this.metaType.AbstractJsonProperty);
                var thisExpresion = Syntax.ThisDot(f.NameAsProperty);

                var expression = IfStatement(
                    BinaryExpression(SyntaxKind.NotEqualsExpression, f.GetJsonValue(paramName), Syntax.ThisDot(f.NameAsProperty)),
                    ThrowStatement(
                        ObjectCreationExpression(Syntax.GetTypeSyntax(typeof(System.Text.Json.JsonException))).AddArgumentListArguments(
                            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"Invalid {this.metaType.AbstractJsonProperty} specified."))))));
                body.Add(expression);
            }

            body.AddRange(
                (await this.metaType.GetLocalPropertiesAsync()).Where(IsRequired).Select(f =>
                    ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(f.NameAsProperty.Identifier), f.GetJsonValue(paramName))))
                );

            var ctor = ConstructorDeclaration(this.metaType.ClassNameIdentifier)
                .AddModifiers(Token(SyntaxKind.InternalKeyword));

            if (!isSealed)
            {
                ctor = ctor.AddModifiers(Token(SyntaxKind.ProtectedKeyword));
            }

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

        private async Task<MemberDeclarationSyntax> MessageCtorAsync(MetaType directAncestor)
        {
           bool isRequired(MetaProperty p) => !(
                p.IsReadonly ||
                p.IsAbstract ||
                p.HasSetMethod ||
                SymbolEqualityComparer.Default.Equals(p.MetaType.TypeSymbol, context.MessageType));

            var localProperties = (await this.metaType.GetLocalPropertiesAsync()).Where(isRequired);
            var body = Block(localProperties.Select(p => p.PropertyAssignment));

            var allProperties = (await this.metaType.GetAllPropertiesAsync()).Where(isRequired);
            var orderedArguments = allProperties.Where(p => !p.IsNullable).OrderBy(p => p.Name)
                    .Concat(allProperties.Where(p => p.IsNullable).OrderBy(p => p.Name));

            var arguments = (await this.metaType.GetInheritedPropertiesAsync())
               .Where(isRequired);

            var baseAssignment =
                SymbolEqualityComparer.Default.Equals(context.MessageType, directAncestor.TypeSymbol) ?
                    MessageSyntax.DirectDescendentConstructorInitializer :
                    MessageSyntax.ConstructorInitializer(arguments);

            var parameters = MessageSyntax.DefaultParameters.Concat(CreateParameterList(orderedArguments));
            return ConstructorDeclaration(this.metaType.ClassNameIdentifier)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, parameters)))
                .WithInitializer(baseAssignment)
                .WithBody(body);
        }

        private async Task<ConstructorInitializerSyntax> GetBaseAssignmentListAsync(Func<MetaProperty, bool> isRequired)
        {
            var props = await this.metaType.GetInheritedPropertiesAsync();
            if (props.Count > 0)
            {
                return ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, CreateAssignmentList(props.Where(isRequired)));
            }
            return null;
        }

        private async Task<IEnumerable<MemberDeclarationSyntax>> PublicCtorAsync(bool hasAncestor)
        {
            bool isRequired(MetaProperty p) => !(
                p.IsReadonly ||
                p.IsAbstract ||
                (p.IsNullable && p.HasSetMethod) ||
                SymbolEqualityComparer.Default.Equals(p.MetaType.TypeSymbol, context.MessageType));

            IEnumerable<ParameterSyntax> CreateParameterList(IEnumerable<MetaProperty> properties)
            {
                ParameterSyntax SetTypeAndDefault(ParameterSyntax parameter, MetaProperty property)
                {
                    if (property.IsNullable)
                    {
                        return parameter.WithType(property.TypeSyntax).WithDefault(EqualsValueClause((ExpressionSyntax)Syntax.Generator.NullLiteralExpression()));
                    }
                    if (property.IsOptional)
                    {
                        switch (property.Name)
                        {
                            case "PartitionKey" when string.Compare(property.ElementType.MetadataName, "string", StringComparison.InvariantCultureIgnoreCase) == 0:
                                ExpressionSyntax defaultPartion = LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"{this.metaType.ClassName.Identifier.ValueText.Replace("Definition", "")}"));
                                return parameter.WithType(property.TypeSyntax).WithDefault(EqualsValueClause(defaultPartion));
                        }
                    }
                    return parameter.WithType(property.TypeSyntax);
                }

                return properties.Select(f => SetTypeAndDefault(SyntaxFactory.Parameter(f.NameAsArgument.Identifier), f));
            }


            var localProperties = (await this.metaType.GetLocalPropertiesAsync()).Where(isRequired);
            var body = Block(localProperties.Select(p => p.PropertyAssignment));

            var allProperties = (await this.metaType.GetAllPropertiesAsync()).Where(isRequired);
            var orderedArguments = allProperties.Where(p => !p.IsOptional).OrderBy(p => p.Name)
                    .Concat(allProperties.Where(p => p.IsOptional).OrderBy(p => p.Name));

            var ctor = ConstructorDeclaration(this.metaType.ClassNameIdentifier)
                .AddModifiers(Token(this.metaType.HasAbstractJsonProperty ? SyntaxKind.ProtectedKeyword : SyntaxKind.PublicKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, CreateParameterList(orderedArguments))))
                .WithBody(body);

            if (hasAncestor)
            {
                var baseAssignment = await GetBaseAssignmentListAsync(isRequired);
                if (baseAssignment != null)
                {
                    ctor = ctor.WithInitializer(baseAssignment);
                }
            }
            return new[] { ctor };
        }

        static class ResponseMessageSyntax
        {
            private static readonly IdentifierNameSyntax requestParam = IdentifierName("resquest");
            private static readonly IdentifierNameSyntax errorProperty = IdentifierName("Error");
            private static readonly ParameterSyntax requestParamType = Parameter(requestParam.Identifier).WithType(ParseName("Chorus.Messaging.IRequestMessage"));

            private static readonly IdentifierNameSyntax requestIdParam = IdentifierName("requestId");
            private static readonly ParameterSyntax requestIdParamType = Parameter(requestIdParam.Identifier).WithType(ParseName(typeof(int).FullName));

            private static readonly IdentifierNameSyntax hasMoreParam = IdentifierName("hasMore");
            private static readonly ParameterSyntax hasMoreParamType = Parameter(hasMoreParam.Identifier).WithType(NullableType(ParseName(typeof(bool).FullName)))
                    .WithDefault(EqualsValueClause((ExpressionSyntax)Syntax.Generator.NullLiteralExpression()));

            private static readonly IdentifierNameSyntax exceptionParam = IdentifierName("error");
            private static readonly ParameterSyntax exceptionParamType = Parameter(exceptionParam.Identifier).WithType(ParseName(typeof(Exception).FullName));

            public static IEnumerable<ParameterSyntax> RequiredParameters = new[] { requestParamType };
            public static IEnumerable<ParameterSyntax> OptionalParameters = new[] { hasMoreParamType };
            public static IEnumerable<IdentifierNameSyntax> AssignmentArguments = new[] { requestParam, hasMoreParam };

            public static readonly MemberAccessExpressionSyntax DotActorId = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, requestParam, MessageSyntax.ActorId);
            private static readonly ArgumentListSyntax messageCtorAssignment = ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, Argument(NameColon(MessageSyntax.CorrelationParameterName), NoneToken, requestParam), Argument(NameColon(MessageSyntax.ActorIdParameterName), NoneToken, DotActorId)));


            public static MemberDeclarationSyntax DefauiltErrorCtor(MetaType sourceMetaType)
            {
                return ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { requestParamType, exceptionParamType })))
                    .WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, CreateAssignmentList(new[] { requestParam, exceptionParam })))
                    .WithBody(Block());
            }

            public static async Task<IEnumerable<MemberDeclarationSyntax>> AbstractResponseCtorAsync(MetaType sourceMetaType)
            {

                var localProperties = (await sourceMetaType.GetLocalPropertiesAsync()).Where(p => p.Name == "HasMore");

                var ctor1 = ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                   .AddModifiers(Token(SyntaxKind.PublicKeyword))
                   .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { requestParamType, hasMoreParamType })))
                   .WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, messageCtorAssignment))
                   .WithBody(Block(localProperties.Select(p => p.PropertyAssignment)));


                var responseError = ObjectCreationExpression(Syntax.GetTypeSyntax("Chorus.Messaging", "ResponseError"))
                    .AddArgumentListArguments(Argument(exceptionParam));

                var ctor2 = ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { requestParamType, exceptionParamType })))
                    .WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, messageCtorAssignment))
                    .WithBody(Block().AddStatements(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, errorProperty, responseError))));

                return new[] { ctor1, ctor2 };
            }

        }

        private async Task<IEnumerable<MemberDeclarationSyntax>> ResponseCtorAsync()
        {
            static IEnumerable<ParameterSyntax> CreateParameterList(IEnumerable<MetaProperty> properties)
            {
                return properties.Select(f => Parameter(f.NameAsArgument.Identifier).WithType(f.Type.GetFullyQualifiedSymbolName(NullableAnnotation.NotAnnotated)));
            }

            bool isRequired(MetaProperty p) => !(
                p.IsReadonly ||
                p.IsAbstract ||
                p.HasSetMethod ||
                SymbolEqualityComparer.Default.Equals(p.MetaType.TypeSymbol, context.ResponseMessageType) ||
                SymbolEqualityComparer.Default.Equals(p.MetaType.TypeSymbol, context.MessageType));


            var localProperties = (await this.metaType.GetLocalPropertiesAsync()).Where(isRequired);
            var body = Block(localProperties.Select(p => p.PropertyAssignment));

            var allProperties = (await this.metaType.GetAllPropertiesAsync()).Where(isRequired);

            var parameters = ResponseMessageSyntax.RequiredParameters
                    .Concat(CreateParameterList(allProperties.Where(p => !p.HasSetMethod).OrderBy(p => p.Name)))
                    .Concat(ResponseMessageSyntax.OptionalParameters);

            var ctor = ConstructorDeclaration(this.metaType.ClassNameIdentifier)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, parameters)))
               .WithBody(body);

            var props = (await this.metaType.GetInheritedPropertiesAsync())
                .Where(isRequired)
                .OrderBy(p => p.Name)
                .Select(p => p.NameAsArgument);

            props = ResponseMessageSyntax.AssignmentArguments.Concat(props);

            var arguments = CreateAssignmentList(props);
            ctor = ctor.WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments));

            return new[] { ctor, ResponseMessageSyntax.DefauiltErrorCtor(this.metaType) };
        }


        private async Task<MethodDeclarationSyntax> ToJsonMethodAsync(bool isSealed)
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

            var properties = await this.metaType.GetLocalPropertiesAsync();
            var body = new List<ExpressionStatementSyntax>();

            var hasAncestor = await this.metaType.HasAncestorAsync();
            if (hasAncestor)
            {
                var ancestor = await this.metaType.GetDirectAncestorAsync();
                var callAncestor = InvocationExpression(Syntax.BaseDot(IdentifierName($"ToJson"))
                           .WithOperatorToken(Token(SyntaxKind.DotToken)))
                           .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(_jsonWriterParameterName)))
                               .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                               .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
                body.Add(ExpressionStatement(callAncestor));
            }

            body.AddRange(properties.Where(IsRequired).Select(f => ExpressionStatement(WriteJsonValue(f))));

            var result = MethodDeclaration(_voidTypeSyntax, Identifier("ToJson"))
                .AddModifiers(Token(SyntaxKind.PublicKeyword));

            if (hasAncestor)
            {
                result = result.AddModifiers(Token(SyntaxKind.OverrideKeyword));
            }
            else if (!isSealed)
            {
                result = result.AddModifiers(Token(SyntaxKind.VirtualKeyword));
            }
            return result.WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _utf8JsonWriterParameter })))
                .WithBody(Block(body));
        }

        internal static class MessageSyntax
        {
            public static readonly IdentifierNameSyntax ActorId = IdentifierName("ActorId");

            public static readonly IdentifierNameSyntax ActorIdParameterName = IdentifierName("actorId");

            private static readonly IdentifierNameSyntax CorrelationId = IdentifierName("CorrelationId");

            public static readonly IdentifierNameSyntax CorrelationParameterName = IdentifierName("correlation");

            private static readonly IdentifierNameSyntax Id = IdentifierName("Id");

            private static readonly IdentifierNameSyntax PostedTime = IdentifierName("PostedTime");

            private static readonly IdentifierNameSyntax TargetParameterName = IdentifierName("target");

            private static readonly IdentifierNameSyntax UserId = IdentifierName("UserId");

            private static readonly IdentifierNameSyntax GuidType = IdentifierName(typeof(Guid).FullName);

            private static readonly ParameterSyntax ActorIdParameterType = Parameter(ActorIdParameterName.Identifier).WithType(GuidType);

            private static readonly ParameterSyntax CorrelatingParamType = Parameter(CorrelationParameterName.Identifier).WithType(ParseName("Chorus.Messaging.IMessage"));

            private static readonly MemberAccessExpressionSyntax DotActorId = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, TargetParameterName, Id);

            private static IEnumerable<ArgumentSyntax> DirectDescendentArguments = new[] { Argument(NameColon(CorrelationParameterName), NoneToken, CorrelationParameterName), Argument(NameColon(ActorIdParameterName), NoneToken, DotActorId) };

            private static readonly ArgumentListSyntax DirectDescendentArgumentList = ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, DirectDescendentArguments));

            private static readonly ParameterSyntax TargetParamType = Parameter(TargetParameterName.Identifier).WithType(ParseName("Chorus.Actors.IVirtualActor"));

            private static IEnumerable<ArgumentSyntax> DescendentArguments = new[] { Argument(NameColon(CorrelationParameterName), NoneToken, CorrelationParameterName), Argument(NameColon(TargetParameterName), NoneToken, TargetParameterName) };

            private static readonly ArgumentListSyntax DescendentArgumentList = ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, DescendentArguments));

            public static readonly IEnumerable<ParameterSyntax> DefaultParameters = new[] { CorrelatingParamType, TargetParamType };

            private static readonly MemberAccessExpressionSyntax DotId = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, CorrelationParameterName, Id);

            private static readonly MemberAccessExpressionSyntax DotUserId = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, CorrelationParameterName, UserId);

            private static readonly MemberAccessExpressionSyntax UtcNow = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(typeof(DateTimeOffset).FullName), IdentifierName(nameof(DateTimeOffset.UtcNow)));

            private static readonly InvocationExpressionSyntax ToUnixTimeMilliseconds = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, UtcNow, IdentifierName(nameof(DateTimeOffset.ToUnixTimeMilliseconds)))
                                                                       .WithOperatorToken(Token(SyntaxKind.DotToken)))
                                                                       .WithArgumentList(ArgumentList().WithOpenParenToken(Token(SyntaxKind.OpenParenToken)).WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            private static readonly InvocationExpressionSyntax NewGuid = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, GuidType, IdentifierName(nameof(Guid.NewGuid)))
                                                                       .WithOperatorToken(Token(SyntaxKind.DotToken)))
                                                                       .WithArgumentList(ArgumentList().WithOpenParenToken(Token(SyntaxKind.OpenParenToken)).WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            public static ConstructorInitializerSyntax DirectDescendentConstructorInitializer = SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, DirectDescendentArgumentList);

            public static ConstructorInitializerSyntax ConstructorInitializer(IEnumerable<MetaProperty> properties)
            {
                var v = DateTimeOffset.UtcNow;
                var resolver = properties.Select(p => Argument(NameColon(p.NameAsArgument), NoneToken, p.NameAsArgument));
                var arguments = ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, DescendentArguments.Concat(resolver)));
                return SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments);

            }
            private static readonly IEnumerable<ParameterSyntax> BaseParameters = new[] { CorrelatingParamType, ActorIdParameterType };

            private static readonly IEnumerable<ExpressionStatementSyntax> ParameterAssignment = new[]
            {
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Id, NewGuid)),
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, PostedTime, ToUnixTimeMilliseconds)),
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, UserId, DotUserId)),
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, ActorId, ActorIdParameterName)),
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, CorrelationId, DotId)),
            };

            public static MemberDeclarationSyntax AbstractMessageCtor(MetaType sourceMetaType)
            {
                var body = Block(ParameterAssignment);
                return ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                    .AddModifiers(Token(SyntaxKind.ProtectedKeyword))
                    .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, BaseParameters)))
                   .WithBody(body);
            }
        }
    }
}
