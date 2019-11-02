namespace CodeGeneration.Chorus
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
        private static FieldDeclarationSyntax CreateConstMember(TypedConstant constant, string name)
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

        private static async Task<IEnumerable<MemberDeclarationSyntax>> CreateClassDeclarationAsync(MetaType sourceMetaType)
        {
            var innerMembers = new List<MemberDeclarationSyntax>();
            var hasAncestor = await sourceMetaType.HasAncestorAsync();

            var mergedFeatures = new List<FeatureGenerator>();
            mergedFeatures.Merge(new StyleCopCompliance(sourceMetaType));

            PropertyDeclarationSyntax CreatePropertyDeclaration(MetaProperty prop)
            {
                var result = prop.PropertyDeclarationSyntax;
                if (prop.Name == sourceMetaType.AbstractJsonProperty)
                {
                    result = result.AddModifiers(Token(SyntaxKind.AbstractKeyword));
                }
                return result;
            }

            PropertyDeclarationSyntax ArrowPropertyDeclarationSyntax(string name, MetaProperty value)
            {
                var memberAccess = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, sourceMetaType.ClassName, IdentifierName(Identifier(name.ToUpperInvariant())));
                return value.ArrowPropertyDeclarationSyntax(memberAccess);
            }

            async Task<(MetaType type, FieldDeclarationSyntax field, PropertyDeclarationSyntax property)> GetAbstractPropertyAsync(MetaType type)
            {
                var constField = CreateConstMember(sourceMetaType.GetAbstractJsonAttributeValue(type.AbstractAttribute), type.AbstractJsonProperty);
                PropertyDeclarationSyntax property = null;
                if (constField != null)
                {
                    var inheritedProperties = await sourceMetaType.GetInheritedPropertiesAsync();
                    var inheritedProperty = inheritedProperties.SingleOrDefault(p => p.Name == type.AbstractJsonProperty);
                    if (inheritedProperty.IsDefault)
                    {
                        property = null;
                    }
                    else
                    {
                        property = ArrowPropertyDeclarationSyntax(type.AbstractJsonProperty, inheritedProperty)
                            .AddModifiers(Token(SyntaxKind.OverrideKeyword));
                    }
                }
                return (type, constField, property);
            }

            // Select out the fields used to serialize an abstract derived types
            var inheritedMembers = (await sourceMetaType.GetDirectAncestorsAsync())
                .Where(a => a.IsAbstract)
                .ToImmutableArray();

            var abstractImplementations = (await Task.WhenAll(inheritedMembers.Select(GetAbstractPropertyAsync)))
                .Where(v => v.field != null)
                .ToImmutableArray();

            var isAbstract = sourceMetaType.IsAbstract || abstractImplementations.Length < inheritedMembers.Length;

            innerMembers.AddRange(abstractImplementations.Select(v => v.field));
            innerMembers.AddRange(await sourceMetaType.CreateJsonCtorAsync(hasAncestor));
            innerMembers.AddRange(await CreatePublicCtorAsync(sourceMetaType, hasAncestor));
            innerMembers.AddRange(abstractImplementations.Select(v => v.property));
            innerMembers.AddRange((await sourceMetaType.GetLocalPropertiesAsync()).Select(CreatePropertyDeclaration));

            var partialClass = ClassDeclaration(sourceMetaType.ClassNameIdentifier)
                 .AddBaseListTypes(sourceMetaType.SemanticModel.AsFullyQualifiedBaseType((TypeDeclarationSyntax)sourceMetaType.DeclarationSyntax).ToArray())
                 .AddModifiers(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.PartialKeyword))
                 .WithMembers(List(innerMembers));

            if (isAbstract)
            {
                partialClass = partialClass.AddModifiers(Token(SyntaxKind.AbstractKeyword));
            }

            partialClass = mergedFeatures.
                Aggregate(partialClass, (acc, feature) => feature.ProcessApplyToClassDeclaration(acc));

            var outerMembers = List<MemberDeclarationSyntax>();
            outerMembers = outerMembers.Add(partialClass);
            // outerMembers = outerMembers.Add(CreateOrleansSerializer());

            var usingsDirectives = List(new[] {
                UsingDirective(ParseName(typeof(System.Text.Json.JsonElement).Namespace)),
                UsingDirective(ParseName("Chorus.Common.Text.Json")),
            });

            var ns = sourceMetaType.DeclarationSyntax.Ancestors().OfType<NamespaceDeclarationSyntax>().Single().Name.WithoutTrivia();
            var members = NamespaceDeclaration(ns)
                 .WithUsings(usingsDirectives)
                 .WithMembers(outerMembers);

            partialClass = members.ChildNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == sourceMetaType.ClassNameIdentifier.ValueText);
            return new[] { members, await CreateJsonSerializerForinterfaceAsync(sourceMetaType, ns) };
        }

        private static async Task<IEnumerable<MemberDeclarationSyntax>> CreateJsonCtorAsync(this MetaType sourceMetaType, bool hasAncestor)
        {
            var paramName = IdentifierName("json");
            var param = Parameter(paramName.Identifier).WithType(ParseName(nameof(System.Text.Json.JsonElement)));

            InvocationExpressionSyntax GetJsonValue(MetaProperty metaProperty)
            {
                var nullable = metaProperty.IsNullable ? "Try" : "";
                var array = metaProperty.IsCollection ? "Array" : "";
                var typeName = metaProperty.TypeClassName;
                return InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, paramName, IdentifierName($"{nullable}Get{typeName}{array}"))
                           .WithOperatorToken(Token(SyntaxKind.DotToken)))
                           .WithArgumentList(
                               ArgumentList(
                                   SingletonSeparatedList(
                                       Argument(
                                           LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(TriviaList(), metaProperty.NameAsJsonProperty, string.Empty, TriviaList()))
                                           )
                                       )
                                   )
                               .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                               .WithCloseParenToken(Token(SyntaxKind.CloseParenToken))
                               );
            }

            var body = new List<StatementSyntax>();

            if (sourceMetaType.IsAbstract)
            {
                var f = (await sourceMetaType.GetLocalPropertiesAsync()).Single(p => p.Name == sourceMetaType.AbstractJsonProperty);
                var thisExpresion = Syntax.ThisDot(f.NameAsProperty);

                var expression = IfStatement(
                    BinaryExpression(SyntaxKind.NotEqualsExpression, GetJsonValue(f), Syntax.ThisDot(f.NameAsProperty)),
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
                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(f.NameAsProperty.Identifier), GetJsonValue(f))))
                );


            var ctor = ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                .AddModifiers(Token(sourceMetaType.IsAbstract ? SyntaxKind.ProtectedKeyword : SyntaxKind.PublicKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { param })))
                // .AddAttributeLists(SyntaxFactory.AttributeList().AddAttributes(ObsoletePublicCtor))
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
            static bool isRequired(MetaProperty p) => !p.IsReadonly && !p.IsAbstract && !p.HasSetMethod;

            var body = Block(
                (await sourceMetaType.GetLocalPropertiesAsync()).Where(isRequired).Select(f => ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(f.NameAsProperty.Identifier),
                        IdentifierName(f.NameAsArgument.Identifier)))));


            // var thisArguments = CreateArgumentList(_applyFromMetaType.AllProperties);
            var ctor = ConstructorDeclaration(sourceMetaType.ClassNameIdentifier)
                .AddModifiers(Token(sourceMetaType.IsAbstract ? SyntaxKind.ProtectedKeyword : SyntaxKind.PublicKeyword))
                .WithParameterList(CreateParameterList((await sourceMetaType.GetAllPropertiesAsync()).Where(isRequired)))
                .WithBody(body);

            if (hasAncestor)
            {
                var props = (await sourceMetaType.GetInheritedPropertiesAsync()).Where(isRequired).ToList();
                if (props.Count > 0)
                {
                    var arguments = CreateAssignmentList(props);
                    ctor = ctor.WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments));
                }
            }
            return new[] { ctor };
        }
    }
}
