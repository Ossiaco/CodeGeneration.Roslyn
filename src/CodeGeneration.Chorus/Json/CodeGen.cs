namespace CodeGeneration.Chorus.Json
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal partial class CodeGen
    {
        private static readonly IdentifierNameSyntax DefaultInstanceFieldName = SyntaxFactory.IdentifierName("DefaultInstance");
        private static readonly SyntaxToken NoneToken = SyntaxFactory.Token(SyntaxKind.None);
        private static readonly AttributeSyntax PureAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(typeof(System.Diagnostics.Contracts.PureAttribute).FullName));
        public static readonly AttributeListSyntax PureAttributeList = SyntaxFactory.AttributeList().AddAttributes(PureAttribute);
        private static ImmutableHashSet<INamedTypeSymbol> CheckedTypes = ImmutableHashSet<INamedTypeSymbol>.Empty;

        private readonly MetaType metaType;
        private readonly ITransformationContext context;
        public CodeGen (MetaType metaType, ITransformationContext transformationContext)
        {
            this.metaType = metaType;
            this.context = transformationContext;
        }

        private static DiagnosticDescriptor NotSupportedDescriptor => new DiagnosticDescriptor("OCC001",
                    new LocalizableResourceString(nameof(DiagnosticStrings.OCC001), DiagnosticStrings.ResourceManager, typeof(DiagnosticStrings)),
                    new LocalizableResourceString(nameof(DiagnosticStrings.OCC001), DiagnosticStrings.ResourceManager, typeof(DiagnosticStrings)),
                    "Design", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        private static DiagnosticDescriptor UnexpectedDescriptor => new DiagnosticDescriptor("OCC002",
                    new LocalizableResourceString(nameof(DiagnosticStrings.OCC002), DiagnosticStrings.ResourceManager, typeof(DiagnosticStrings)),
                    new LocalizableResourceString(nameof(DiagnosticStrings.OCC002), DiagnosticStrings.ResourceManager, typeof(DiagnosticStrings)),
                    "Design", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static bool IsPartialImplementationOfInterface(MetaType m, MetaType metaType)
        {
            return m.IsPartialClass
                && m.TypeSymbol.Interfaces.Any(i => i.Equals(metaType.TypeSymbol))
                && m.TypeSymbol.ContainingNamespace.Equals(metaType.TypeSymbol.ContainingNamespace);
        }

        internal async Task<ImmutableArray<MemberDeclarationSyntax>> GenerateAsync()
        {
            try
            {
                if (!metaType.IsDefault)
                {
                    var key = metaType.TypeSymbol.OriginalDefinition ?? metaType.TypeSymbol;
                    if (TryAdd(ref CheckedTypes, key))
                    {
                        switch (key.TypeKind)
                        {
                            case TypeKind.Interface when metaType.IsJsonSerializeable && metaType.IsGenericType:
                                context.Progress.Report(Diagnostic.Create(NotSupportedDescriptor, metaType.DeclarationSyntax.GetLocation(), key.TypeKind, key.Name));
                                break;
                            case TypeKind.Interface when metaType.IsJsonSerializeable:
                                var result = ImmutableArray<MemberDeclarationSyntax>.Empty.ToBuilder();
                                var partialImplementation = context.AllNamedTypeSymbols.Values.FirstOrDefault(m => IsPartialImplementationOfInterface(m, metaType));
                                result.Add(await CreateClassDeclarationAsync(partialImplementation));
                                result.Add(await CreateJsonSerializerForinterfaceAsync());
                                return result.ToImmutable();
                            case TypeKind.Enum:
                                return ImmutableArray<MemberDeclarationSyntax>.Empty.Add(CreateEnumSerializerClass());
                            default:
                                context.Progress.Report(Diagnostic.Create(NotSupportedDescriptor, metaType.DeclarationSyntax.GetLocation(), key.TypeKind, key.Name));
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                context.Progress.Report(Diagnostic.Create(UnexpectedDescriptor, metaType.DeclarationSyntax?.GetLocation() ?? Location.None, e.Message));
            }
            return ImmutableArray<MemberDeclarationSyntax>.Empty;
        }

        //private async Task<ImmutableArray<MemberDeclarationSyntax>> GenerateDependenciesAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        //{
        //    var allProperties = await metaType.GetLocalPropertiesAsync();
        //    var dependencies = allProperties.Select(p => p.SafeType).Where(s => !context.IntrinsicSymbols.Contains(s)).ToImmutableArray();
        //    var result = ImmutableArray<MemberDeclarationSyntax>.Empty;
        //    if (dependencies.Length > 0)
        //    {
        //        var tasks = context.AllNamedTypeSymbols.Where(to => dependencies.Contains(to.Key)).Select(to => GenerateAsync(to.Value, context, progress, cancellationToken));
        //        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        //        foreach (var r in results)
        //        {
        //            result = result.AddRange(r);
        //        }
        //    }
        //    return result;
        //}

        private static bool IsAssignableFrom(INamedTypeSymbol from, INamedTypeSymbol to)
        {
            if (to != null)
            {
                return SymbolEqualityComparer.Default.Equals(from, to)
                    || IsAssignableFrom(from, to.BaseType)
                    || to.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(from, i));
            }
            return false;
        }

        private static bool TryAdd<T>(ref ImmutableHashSet<T> set, T value)
        {
            while (true)
            {
                var currentSet = Volatile.Read(ref set);
                var updatedSet = currentSet.Add(value);
                var originalSet = Interlocked.CompareExchange(ref set, updatedSet, currentSet);
                if (originalSet != currentSet)
                {
                    // Try again
                    continue;
                }
                return updatedSet != currentSet;
            }
        }

        //private ClassDeclarationSyntax CreateOrleansSerializer()
        //{
        //    var className = SyntaxFactory.Identifier($"{_applyToTypeName.Identifier.Text}OrleansSerializer");
        //    var partialClass = SyntaxFactory.ClassDeclaration(className)
        //        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

        //    return partialClass;
        //}

        private static SyntaxList<MemberDeclarationSyntax> WrapInAncestor(SyntaxList<MemberDeclarationSyntax> generatedMembers, SyntaxNode ancestor)
        {
            switch (ancestor)
            {
                case NamespaceDeclarationSyntax ancestorNamespace:
                    generatedMembers = SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                        CopyAsAncestor(ancestorNamespace)
                        .WithMembers(generatedMembers));
                    break;
                case ClassDeclarationSyntax nestingClass:
                    generatedMembers = SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                        CopyAsAncestor(nestingClass)
                        .WithMembers(generatedMembers));
                    break;
                case StructDeclarationSyntax nestingStruct:
                    generatedMembers = SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                        CopyAsAncestor(nestingStruct)
                        .WithMembers(generatedMembers));
                    break;
            }
            return generatedMembers;
        }

        private static NamespaceDeclarationSyntax CopyAsAncestor(NamespaceDeclarationSyntax syntax)
        {
            return SyntaxFactory.NamespaceDeclaration(syntax.Name.WithoutTrivia())
                .WithExterns(SyntaxFactory.List(syntax.Externs.Select(x => x.WithoutTrivia())))
                .WithUsings(SyntaxFactory.List(syntax.Usings.Select(x => x.WithoutTrivia())));
        }

        private static ClassDeclarationSyntax CopyAsAncestor(ClassDeclarationSyntax syntax)
        {
            return SyntaxFactory.ClassDeclaration(syntax.Identifier.WithoutTrivia())
                .WithModifiers(SyntaxFactory.TokenList(syntax.Modifiers.Select(x => x.WithoutTrivia())))
                .WithTypeParameterList(syntax.TypeParameterList);
        }

        private static StructDeclarationSyntax CopyAsAncestor(StructDeclarationSyntax syntax)
        {
            return SyntaxFactory.StructDeclaration(syntax.Identifier.WithoutTrivia())
                .WithModifiers(SyntaxFactory.TokenList(syntax.Modifiers.Select(x => x.WithoutTrivia())))
                .WithTypeParameterList(syntax.TypeParameterList);
        }

        private static PropertyDeclarationSyntax CreateProperty(FieldDeclarationSyntax field, VariableDeclaratorSyntax variable)
        {
            var xmldocComment = field.GetLeadingTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

            var property = SyntaxFactory.PropertyDeclaration(field.Declaration.Type, variable.Identifier.ValueText.ToPascalCase())
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(
                        // => this.fieldName
                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ThisExpression(), SyntaxFactory.IdentifierName(variable.Identifier))))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(xmldocComment); // TODO: modify the <summary> to translate "Some description" to "Gets some description."
            return property;
        }


        private static IEnumerable<MetaProperty> SortRequiredPropertiesFirst(IEnumerable<MetaProperty> fields)
        {
            return fields.Where(f => f.IsNonNullable).Concat(fields.Where(f => !f.IsNonNullable));
        }

        private static IEnumerable<ParameterSyntax> CreateParameterList(IEnumerable<MetaProperty> properties)
        {
            properties = SortRequiredPropertiesFirst(properties);

            ParameterSyntax SetTypeAndDefault(ParameterSyntax p, MetaProperty f) => f.IsNullable
                 ? p.WithType(f.TypeSyntax).WithDefault(SyntaxFactory.EqualsValueClause((ExpressionSyntax)Syntax.Generator.NullLiteralExpression()))
                 : p.WithType(f.TypeSyntax);

            return properties.Select(f => SetTypeAndDefault(SyntaxFactory.Parameter(f.NameAsArgument.Identifier), f));
        }

        private static ArgumentListSyntax CreateArgumentList(IEnumerable<MetaProperty> properties)
        {
            return SyntaxFactory.ArgumentList(Syntax.JoinSyntaxNodes(
                           SyntaxKind.CommaToken,
                           properties.Select(f =>
                               SyntaxFactory.Argument(
                                   SyntaxFactory.NameColon(f.NameAsArgument),
                                   NoneToken,
                                   Syntax.ThisDot(f.NameAsProperty)))));
        }
        private static ArgumentListSyntax CreateAssignmentList(IEnumerable<IdentifierNameSyntax> properties)
            => SyntaxFactory.ArgumentList(Syntax.JoinSyntaxNodes(SyntaxKind.CommaToken, properties.Select(f => SyntaxFactory.Argument(SyntaxFactory.NameColon(f), NoneToken, f))));

        private static ArgumentListSyntax CreateAssignmentList(IEnumerable<MetaProperty> properties) => CreateAssignmentList(properties.Select(p => p.NameAsArgument));
    }
}
