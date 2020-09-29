//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------
#nullable enable

namespace CodeGeneration.Chorus.Json
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

    internal partial class CodeGen
    {
        private static readonly BaseExpressionSyntax _baseExpression = BaseExpression(Token(SyntaxKind.BaseKeyword));

        public static FieldDeclarationSyntax? CreateConstMember(TypedConstant constant, string name)
        {
            if (constant.Kind != TypedConstantKind.Error)
            {
                var syntax = constant.Type.GetFullyQualifiedSymbolName(NullableAnnotation.None);
                var value = Syntax.Generator.TypedConstantExpression(constant);
                if (syntax != null && value is ExpressionSyntax expr)
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
            var properties = await this.metaType.GetLocalPropertiesAsync();


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
            var isAbstract = await this.metaType.IsAbstractTypeAsync();
            var isSealed = isAbstract ? false : partialImplementation?.TypeSymbol.IsSealed ?? false || !(await this.metaType.HasDescendentsAsync());
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
                innerMembers.Add(await ToJsonMethodAsync(isSealed, properties));
            }

            innerMembers.AddRange(IEquatableImplementation(properties, directAncestor));

            var partialClass = ClassDeclaration(this.metaType.ClassNameIdentifier)
                 .AddBaseListTypes(this.metaType.SemanticModel.AsFullyQualifiedBaseType(this.metaType).ToArray())
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

        private TupleExpressionSyntax GetAsTuple(ImmutableHashSet<MetaProperty> properties, ExpressionSyntax? expreession = null)
        {
            expreession = expreession ?? ThisExpression();

            ArgumentSyntax GetTupleArgument(MetaProperty metaProperty) => Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expreession, IdentifierName(metaProperty.Symbol.Name)));
            var arguments = properties.Select(GetTupleArgument);

            // If all the types are nullable then a literal value is added to the tuple
            if (properties.All(p => p.IsNullable))
            {
                arguments = (new[] { Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))) }).Concat(arguments);
            }
            return TupleExpression().AddArguments(arguments.ToArray());
        }

        private async Task<ConstructorInitializerSyntax?> GetBaseAssignmentListAsync(Func<MetaProperty, bool> isRequired)
        {
            var props = await this.metaType.GetInheritedPropertiesAsync();
            if (props.Count > 0)
            {
                return ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, CreateAssignmentList(props.Where(isRequired)));
            }
            return null;
        }

        private IEnumerable<MemberDeclarationSyntax> IEquatableImplementation(ImmutableHashSet<MetaProperty> properties, MetaType ancestor)
        {
            var aIdentifierName = IdentifierName("a");
            var bIdentifierName = IdentifierName("b");
            var baseIdentifierName = IdentifierName("base");
            var equalsIdentifierName = IdentifierName("Equals");
            var getHashCodeIdentifierName = IdentifierName("GetHashCode");

            var ancestorType = ancestor.IsDefault ? _objectType : ancestor.TypeSyntax;
            var interfaceType = ParseName(this.metaType.DeclarationSyntax.Identifier.ValueText);
            var classType = ParseName(this.metaType.ClassNameIdentifier.ValueText);
            ExpressionSyntax whenFalse = LiteralExpression(SyntaxKind.FalseLiteralExpression, Token(SyntaxKind.FalseKeyword));

            ExpressionSyntax whenTrue = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("this"), equalsIdentifierName)
                   .WithOperatorToken(Token(SyntaxKind.DotToken)))
                   .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(_valueName) }))
                       .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                       .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            var conditionalExpression = ConditionalExpression(IsPatternExpression(_objName, DeclarationPattern(interfaceType, SingleVariableDesignation(_valueIdentifier))), whenTrue, whenFalse);

            // public override bool Equals(object? obj) => obj is <interfaceType> value ? this.Equals(value) : false;
            yield return MethodDeclaration(_boolType, _equalsIdentifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword))
                    .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { _nullableObjectParameter })))
                    .WithExpressionBody(ArrowExpressionClause(conditionalExpression))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            conditionalExpression = ConditionalExpression(IsPatternExpression(_otherName, DeclarationPattern(interfaceType, SingleVariableDesignation(_valueIdentifier))), whenTrue, whenFalse);

            // public override bool Equals(<classType> other) => other is <interfaceType> value ? this.Equals(value) : false;
            yield return MethodDeclaration(_boolType, _equalsIdentifier)
                   .AddModifiers(Token(SyntaxKind.PublicKeyword))
                   .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Parameter(_otherName.Identifier).WithType(classType) })))
                   .WithExpressionBody(ArrowExpressionClause(conditionalExpression))
                   .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));


            var baseEquals = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, baseIdentifierName, equalsIdentifierName)
                   .WithOperatorToken(Token(SyntaxKind.DotToken)))
                   .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(CastExpression(ancestorType, _valueName)) }))
                       .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                       .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            if (properties.Count > 0)
            {
                whenTrue = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, GetAsTuple(properties), equalsIdentifierName)
                   .WithOperatorToken(Token(SyntaxKind.DotToken)))
                   .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(GetAsTuple(properties, _otherName)) }))
                       .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                       .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

                if (!ancestor.IsDefault)
                {
                    whenTrue = BinaryExpression(SyntaxKind.LogicalAndExpression, baseEquals, whenTrue);
                }
            }
            else if (ancestor.IsDefault)
            {
                whenTrue = LiteralExpression(SyntaxKind.FalseLiteralExpression, Token(SyntaxKind.TrueKeyword));
            }
            else
            {
                whenTrue = baseEquals;
            }

            conditionalExpression = ConditionalExpression(IsPatternExpression(_otherName, DeclarationPattern(interfaceType, SingleVariableDesignation(_valueIdentifier))), whenTrue, whenFalse);

            // public override bool Equals(<interfaceType> other) => other is <interfaceType> value ? <tuple.Equals> : false;
            yield return MethodDeclaration(_boolType, _equalsIdentifier)
                   .AddModifiers(Token(SyntaxKind.PublicKeyword))
                   .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Parameter(_otherName.Identifier).WithType(interfaceType) })))
                   .WithExpressionBody(ArrowExpressionClause(conditionalExpression))
                   .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));


            whenTrue = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, GetAsTuple(properties), getHashCodeIdentifierName)
                .WithOperatorToken(Token(SyntaxKind.DotToken)));

            var baseGetHashCode = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, baseIdentifierName, getHashCodeIdentifierName)
               .WithOperatorToken(Token(SyntaxKind.DotToken)));

            if (ancestor.IsDefault || properties.Count == 0)
            {
                if (properties.Count == 0)
                {
                    whenTrue = baseGetHashCode;
                }

                // public override int GetHashCode() => <tuple>.GetHashCode();
                yield return MethodDeclaration(_intType, _getHashCodeIdentifier)
                       .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword))
                       .WithExpressionBody(ArrowExpressionClause(whenTrue))
                       .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }
            else
            {
                var multiply = BinaryExpression(SyntaxKind.MultiplyExpression, baseGetHashCode, LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(16777619)));
                var body = CheckedStatement(SyntaxKind.UncheckedStatement, Block(ReturnStatement(BinaryExpression(SyntaxKind.AddExpression, multiply, whenTrue))));

                yield return MethodDeclaration(_intType, _getHashCodeIdentifier)
                      .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword))
                      .WithBody(Block(body));
            }


            // a.Equals(b)
            whenTrue = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, aIdentifierName, equalsIdentifierName)
                  .WithOperatorToken(Token(SyntaxKind.DotToken)))
                  .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(bIdentifierName) }))
                      .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                      .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            // object.Equals(a, b)
            whenFalse = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _objectType, equalsIdentifierName)
                   .WithOperatorToken(Token(SyntaxKind.DotToken)))
                   .WithArgumentList(ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Argument(aIdentifierName), Argument(bIdentifierName) }))
                       .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                       .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

            // a is <interface> && b is <interface> ? !a.Equals(b) : !object.Equals(a, b)
            conditionalExpression = ConditionalExpression(BinaryExpression(SyntaxKind.LogicalAndExpression,
                    BinaryExpression(SyntaxKind.IsExpression, aIdentifierName, interfaceType),
                    BinaryExpression(SyntaxKind.IsExpression, bIdentifierName, interfaceType)),
                    whenTrue, whenFalse);

            // public static bool operator ==(<classType>? a, <interfaceType>? b) => a is <interfaceType> && b is <interfaceType> ? a.Equals(b) : object.Equals(a, b);
            yield return OperatorDeclaration(_boolType, Token(SyntaxKind.EqualsEqualsToken))
               .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
               .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Parameter(aIdentifierName.Identifier).WithType(NullableType(classType)), Parameter(bIdentifierName.Identifier).WithType(NullableType(interfaceType)) })))
               .WithExpressionBody(ArrowExpressionClause(conditionalExpression))
               .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));


            whenTrue = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, whenTrue);
            whenFalse = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, whenFalse);
            conditionalExpression = ConditionalExpression(BinaryExpression(SyntaxKind.LogicalAndExpression,
                    BinaryExpression(SyntaxKind.IsExpression, aIdentifierName, interfaceType),
                    BinaryExpression(SyntaxKind.IsExpression, bIdentifierName, interfaceType)),
                    whenTrue, whenFalse);

            // public static bool operator ==(<classType>? a, <interfaceType>? b) => a is <interfaceType> && b is <interfaceType> ? !a.Equals(b) : !object.Equals(a, b);
            yield return OperatorDeclaration(_boolType, Token(SyntaxKind.ExclamationEqualsToken))
               .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
               .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { Parameter(aIdentifierName.Identifier).WithType(NullableType(classType)), Parameter(bIdentifierName.Identifier).WithType(NullableType(interfaceType)) })))
               .WithExpressionBody(ArrowExpressionClause(conditionalExpression))
               .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

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
                                ExpressionSyntax defaultPartion = LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"{this.metaType.ClassName.Identifier.ValueText.Replace("Definition", string.Empty)}"));
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

            return new[] { ctor, ResponseMessageSyntax.DefaultErrorCtor(this.metaType) };
        }

        private async Task<MethodDeclarationSyntax> ToJsonMethodAsync(bool isSealed, ImmutableHashSet<MetaProperty> properties)
        {

            static InvocationExpressionSyntax WriteJsonValue(MetaProperty metaProperty)
            {
                var nullable = metaProperty.IsNullable ? "Safe" : string.Empty;
                var array = metaProperty.IsCollection ? "Array" : string.Empty;
                var typeName = metaProperty.JsonTypeName;
                return InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _writerName, IdentifierName($"{nullable}Write{typeName}{array}"))
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

            var body = new List<ExpressionStatementSyntax>();

            var hasAncestor = await this.metaType.HasAncestorAsync();
            if (hasAncestor)
            {
                var ancestor = await this.metaType.GetDirectAncestorAsync();
                var callAncestor = InvocationExpression(Syntax.BaseDot(IdentifierName($"ToJson"))
                           .WithOperatorToken(Token(SyntaxKind.DotToken)))
                           .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(_writerName)))
                               .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                               .WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
                body.Add(ExpressionStatement(callAncestor));
            }

            body.AddRange(properties.Where(IsRequired).Select(f => ExpressionStatement(WriteJsonValue(f))));

            var result = MethodDeclaration(_voidType, Identifier("ToJson"))
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
            public static readonly IdentifierNameSyntax ActorId;

            public static readonly IdentifierNameSyntax ActorIdParameterName;

            public static readonly IdentifierNameSyntax CorrelationParameterName;

            public static readonly IEnumerable<ParameterSyntax> DefaultParameters;

            public static ConstructorInitializerSyntax DirectDescendentConstructorInitializer;

            private static readonly ParameterSyntax ActorIdParameterType;
            private static readonly IEnumerable<ParameterSyntax> BaseParameters;
            private static readonly ParameterSyntax CorrelatingParamType;
            private static readonly IdentifierNameSyntax CorrelationId;
            private static readonly IEnumerable<ArgumentSyntax> DescendentArguments;
            private static readonly ArgumentListSyntax DirectDescendentArgumentList;
            private static readonly IEnumerable<ArgumentSyntax> DirectDescendentArguments;
            private static readonly MemberAccessExpressionSyntax DotId;
            private static readonly MemberAccessExpressionSyntax DotUserId;
            private static readonly IdentifierNameSyntax GuidType;
            private static readonly IdentifierNameSyntax Id;
            private static readonly InvocationExpressionSyntax NewGuid;
            private static readonly IEnumerable<ExpressionStatementSyntax> ParameterAssignment;
            private static readonly IdentifierNameSyntax PostedTime;
            private static readonly InvocationExpressionSyntax ToUnixTimeMilliseconds;
            private static readonly IdentifierNameSyntax UserId;
            private static readonly MemberAccessExpressionSyntax UtcNow;

            static MessageSyntax()
            {
                ActorId = IdentifierName("ActorId");
                ActorIdParameterName = IdentifierName("actorId");
                CorrelationId = IdentifierName("CorrelationId");
                CorrelationParameterName = IdentifierName("correlation");
                Id = IdentifierName("Id");
                PostedTime = IdentifierName("PostedTime");
                UserId = IdentifierName("UserId");
                GuidType = IdentifierName(typeof(Guid).FullName);
                ActorIdParameterType = Parameter(ActorIdParameterName.Identifier).WithType(GuidType);
                CorrelatingParamType = Parameter(CorrelationParameterName.Identifier).WithType(ParseName("Chorus.Messaging.IMessage"));
                DirectDescendentArguments = new[] { Argument(NameColon(CorrelationParameterName), NoneToken, CorrelationParameterName), Argument(NameColon(ActorIdParameterName), NoneToken, ActorIdParameterName) };
                DirectDescendentArgumentList = ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, DirectDescendentArguments));
                DescendentArguments = new[] { Argument(NameColon(CorrelationParameterName), NoneToken, CorrelationParameterName), Argument(NameColon(ActorIdParameterName), NoneToken, ActorIdParameterName) };
                DefaultParameters = new[] { CorrelatingParamType, ActorIdParameterType };
                DotId = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, CorrelationParameterName, Id);
                DotUserId = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, CorrelationParameterName, UserId);
                UtcNow = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(typeof(DateTimeOffset).FullName), IdentifierName(nameof(DateTimeOffset.UtcNow)));

                ToUnixTimeMilliseconds = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, UtcNow, IdentifierName(nameof(DateTimeOffset.ToUnixTimeMilliseconds)))
                                                                           .WithOperatorToken(Token(SyntaxKind.DotToken)))
                                                                           .WithArgumentList(ArgumentList().WithOpenParenToken(Token(SyntaxKind.OpenParenToken)).WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));

                NewGuid = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, GuidType, IdentifierName(nameof(Guid.NewGuid)))
                                                                           .WithOperatorToken(Token(SyntaxKind.DotToken)))
                                                                           .WithArgumentList(ArgumentList().WithOpenParenToken(Token(SyntaxKind.OpenParenToken)).WithCloseParenToken(Token(SyntaxKind.CloseParenToken)));
                ParameterAssignment = new[]
                {
                    ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Id, NewGuid)),
                    ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, PostedTime, ToUnixTimeMilliseconds)),
                    ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, UserId, DotUserId)),
                    ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, ActorId, ActorIdParameterName)),
                    ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, CorrelationId, DotId)),
                };

                BaseParameters = new[] { CorrelatingParamType, ActorIdParameterType };


                DirectDescendentConstructorInitializer = SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, DirectDescendentArgumentList);
            }

            public static MemberDeclarationSyntax AbstractMessageCtor(MetaType sourceMetaType)
            {
                var body = Block(ParameterAssignment);
                return ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                    .AddModifiers(Token(SyntaxKind.ProtectedKeyword))
                    .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, BaseParameters)))
                   .WithBody(body);
            }

            public static ConstructorInitializerSyntax ConstructorInitializer(IEnumerable<MetaProperty> properties)
            {
                var v = DateTimeOffset.UtcNow;
                var resolver = properties.Select(p => Argument(NameColon(p.NameAsArgument), NoneToken, p.NameAsArgument));
                var arguments = ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, DescendentArguments.Concat(resolver)));
                return SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments);

            }
        }

        static class ResponseMessageSyntax
        {
            public static readonly MemberAccessExpressionSyntax DotActorId;

            public static IEnumerable<IdentifierNameSyntax> AssignmentArguments;

            public static IEnumerable<ParameterSyntax> OptionalParameters;

            public static IEnumerable<ParameterSyntax> RequiredParameters;

            private static readonly IdentifierNameSyntax errorProperty;
            private static readonly IdentifierNameSyntax exceptionParam;
            private static readonly ParameterSyntax exceptionParamType;
            private static readonly IdentifierNameSyntax hasMoreParam;
            private static readonly ParameterSyntax hasMoreParamType;
            private static readonly ArgumentListSyntax messageCtorAssignment;
            private static readonly IdentifierNameSyntax requestIdParam;
            private static readonly ParameterSyntax requestIdParamType;
            private static readonly IdentifierNameSyntax requestParam;
            private static readonly ParameterSyntax requestParamType;

            static ResponseMessageSyntax()
            {
                requestParam = IdentifierName("request");
                errorProperty = IdentifierName("Error");
                requestParamType = Parameter(requestParam.Identifier).WithType(ParseName("Chorus.Messaging.IRequestMessage"));
                requestIdParam = IdentifierName("requestId");
                requestIdParamType = Parameter(requestIdParam.Identifier).WithType(ParseName(typeof(int).FullName));
                hasMoreParam = IdentifierName("hasMore");
                hasMoreParamType = Parameter(hasMoreParam.Identifier).WithType(NullableType(ParseName(typeof(bool).FullName)))
                        .WithDefault(EqualsValueClause((ExpressionSyntax)Syntax.Generator.NullLiteralExpression()));

                exceptionParam = IdentifierName("error");
                exceptionParamType = Parameter(exceptionParam.Identifier).WithType(ParseName("Chorus.Messaging.RequestException"));
                RequiredParameters = new[] { requestParamType };
                OptionalParameters = new[] { hasMoreParamType };
                AssignmentArguments = new[] { requestParam, hasMoreParam };
                DotActorId = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, requestParam, MessageSyntax.ActorId);
                messageCtorAssignment = ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, Argument(NameColon(MessageSyntax.CorrelationParameterName), NoneToken, requestParam), Argument(NameColon(MessageSyntax.ActorIdParameterName), NoneToken, DotActorId)));
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

            public static MemberDeclarationSyntax DefaultErrorCtor(MetaType sourceMetaType)
            {
                return ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { requestParamType, exceptionParamType })))
                    .WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, CreateAssignmentList(new[] { requestParam, exceptionParam })))
                    .WithBody(Block());
            }
        }
    }
}
