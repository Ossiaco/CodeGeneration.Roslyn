namespace CodeGeneration.Chorus
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

    internal partial class CodeGen
    {
        private FieldDeclarationSyntax CreateConstMember(TypedConstant constant, string name)
        {
            var syntax = constant.Type.GetFullyQualifiedSymbolName(NullableAnnotation.None);
            var value = Syntax.Generator.TypedConstantExpression(constant);
            if (value is LiteralExpressionSyntax expr)
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
            throw new System.NotSupportedException();
        }

        private IEnumerable<MemberDeclarationSyntax> CreateClassDeclaration()
        {
            var innerMembers = new List<MemberDeclarationSyntax>();

            var isDerived = InterfaceMetaType.HasAncestor;

            var mergedFeatures = new List<FeatureGenerator>();
            mergedFeatures.Merge(new StyleCopCompliance(this));
            mergedFeatures.Merge(new NullableType(this));

            // Define the constructor after merging all features since they can add to it.

            PropertyDeclarationSyntax CreatePropertyDeclaration(MetaProperty prop)
            {
                var result = prop.PropertyDeclarationSyntax;
                if (prop.Name == _options.AbstractField)
                {
                    result = result.AddModifiers(Token(SyntaxKind.AbstractKeyword));
                }
                return result;
            }

            // Select out the fields used to serializa abstract derived types
            var inheritedMembers = InterfaceMetaType.Ancestors.Select(a => a.AbstractAttributes)
                .Where(a => a.attribute != default)
                .Select(v => (value: InterfaceMetaType.AttributeConstructorValue(v.attribute), field: v.field))
                .Where(a => a.value.Value != default)
                .ToList();

            innerMembers.AddRange(inheritedMembers.Select(v => CreateConstMember(v.value, v.field)));
            innerMembers.AddRange(CreateJsonCtor(isDerived));
            innerMembers.AddRange(CreatePublicCtor(isDerived));


            var inheritedProperties = inheritedMembers
                .Select(a => InterfaceMetaType.InheritedProperties.Single(p => p.Name == a.field).PropertyDeclarationSyntax.AddModifiers(Token(SyntaxKind.OverrideKeyword)));

            innerMembers.AddRange(inheritedProperties);
            innerMembers.AddRange(InterfaceMetaType.LocalProperties.Select(CreatePropertyDeclaration));

            var partialClass = ClassDeclaration(_applyToTypeName.Identifier)
                .AddBaseListTypes(_interfaceDeclaration.AsBaseType().ToArray())
                .AddModifiers(Token(SyntaxKind.InternalKeyword))
                .WithMembers(List(innerMembers));

            if (_options.IsAbstract || _options.AbstractAttributeType != null)
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

            var ns = _interfaceDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().Single().Name.WithoutTrivia();
            var members = NamespaceDeclaration(ns)
                 .WithUsings(usingsDirectives)
                 .WithMembers(outerMembers);

            partialClass = members.ChildNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == _applyToTypeName.Identifier.ValueText);
            // return new[] { members, CreateJsonSerializer(partialClass, ns) };
            return new[] { members };
        }

        private IEnumerable<MemberDeclarationSyntax> CreateJsonCtor(bool hasAncestor)
        {
            var paramName = IdentifierName("json");
            var param = Parameter(paramName.Identifier).WithType(ParseName(nameof(System.Text.Json.JsonElement)));

            InvocationExpressionSyntax GetJsonValue(MetaProperty metaProperty)
            {
                var nullable = metaProperty.IsNullable ? "Try" : "";
                var array = metaProperty.IsCollection ? "Array" : "";
                var typeName = metaProperty.TypeName;
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

            var body = Block(
                InterfaceMetaType.LocalProperties.Where(p => !p.IsIgnored).Select(f =>
                ExpressionStatement(
                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(f.NameAsProperty.Identifier), GetJsonValue(f))))
                );


            var ctor = ConstructorDeclaration(_applyToTypeName.Identifier)
                .AddModifiers(Token(_options.IsAbstract ? SyntaxKind.ProtectedKeyword : SyntaxKind.PublicKeyword))
                .WithParameterList(ParameterList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, new[] { param })))
                // .AddAttributeLists(SyntaxFactory.AttributeList().AddAttributes(ObsoletePublicCtor))
                .WithBody(body);

            if (hasAncestor)
            {
                ctor = ctor.WithInitializer(ConstructorInitializer(
                    SyntaxKind.BaseConstructorInitializer,
                    ArgumentList(SingletonSeparatedList(Argument(paramName)))));
            }

            yield return ctor;
        }

        private IEnumerable<MemberDeclarationSyntax> CreatePublicCtor(bool hasAncestor)
        {
            var body = Block(
                InterfaceMetaType.LocalProperties.Where(p => !p.IsReadonly).Select(f => ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(f.NameAsProperty.Identifier),
                        IdentifierName(f.NameAsArgument.Identifier)))));


            // var thisArguments = CreateArgumentList(_applyFromMetaType.AllProperties);
            var ctor = ConstructorDeclaration(_applyToTypeName.Identifier)
                .AddModifiers(Token(_options.IsAbstract ? SyntaxKind.ProtectedKeyword : SyntaxKind.PublicKeyword))
                .WithParameterList(CreateParameterList(InterfaceMetaType.AllProperties.Where(p => !p.IsReadonly)))
                .WithBody(body);

            if (hasAncestor)
            {
                var props = InterfaceMetaType.InheritedProperties.Where(p => !p.IsReadonly).ToList();
                if (props.Count > 0)
                {
                    var arguments = CreateAssignmentList(props);
                    ctor = ctor.WithInitializer(ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments));
                }
            }
            yield return ctor;
        }


    }
}
