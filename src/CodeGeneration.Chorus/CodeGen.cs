namespace CodeGeneration.Chorus
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CodeGeneration.Roslyn;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal static partial class CodeGen
    {
        private static readonly IdentifierNameSyntax DefaultInstanceFieldName = SyntaxFactory.IdentifierName("DefaultInstance");
        private static readonly SyntaxToken NoneToken = SyntaxFactory.Token(SyntaxKind.None);
        private static readonly SyntaxToken NullToken = SyntaxFactory.Token(SyntaxKind.NullKeyword);
        private static readonly SyntaxToken DotToken = SyntaxFactory.Token(SyntaxKind.DotToken);
        private static readonly IdentifierNameSyntax varType = SyntaxFactory.IdentifierName("var");
        private static readonly IdentifierNameSyntax WithMethodName = SyntaxFactory.IdentifierName("With");
        private static readonly IdentifierNameSyntax WithCoreMethodName = SyntaxFactory.IdentifierName("WithCore");
        private static readonly AttributeSyntax DebuggerBrowsableNeverAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName(typeof(DebuggerBrowsableAttribute).FullName),
            SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseName(typeof(DebuggerBrowsableState).FullName),
                    SyntaxFactory.IdentifierName(nameof(DebuggerBrowsableState.Never)))))));
        private static readonly AttributeSyntax PureAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(typeof(System.Diagnostics.Contracts.PureAttribute).FullName));
        public static readonly AttributeListSyntax PureAttributeList = SyntaxFactory.AttributeList().AddAttributes(PureAttribute);
        private static readonly ThrowStatementSyntax ThrowNotImplementedException = SyntaxFactory.ThrowStatement(SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(typeof(NotImplementedException).FullName), SyntaxFactory.ArgumentList(), null));
        private static readonly AttributeSyntax ObsoletePublicCtor = SyntaxFactory.Attribute(Syntax.GetTypeSyntax(typeof(ObsoleteAttribute))).AddArgumentListArguments(SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("This constructor for use with json deserializers only. Use the static Create factory method instead."))));
        private static ImmutableHashSet<INamedTypeSymbol> CheckedTypes = ImmutableHashSet<INamedTypeSymbol>.Empty;

        internal static ImmutableDictionary<INamedTypeSymbol, MetaType> AllNamedTypeSymbols { get; private set; }
        internal static ImmutableHashSet<INamedTypeSymbol> IntrinsicSymbols { get; private set; }
        internal static ImmutableDictionary<INamedTypeSymbol, string> FormatStrings { get; private set; }
        public static INamedTypeSymbol IJsonSerializeableType;


        private async static Task<ImmutableDictionary<INamedTypeSymbol, MetaType>> GetAllTypeDefinitionsAsync(CSharpCompilation compilation)
        {
            IJsonSerializeableType = compilation.GetTypeByMetadataName(typeof(IJsonSerialize).FullName);
            var result = new ConcurrentDictionary<INamedTypeSymbol, MetaType>(SymbolEqualityComparer.Default);

            async Task TryAdd(INamedTypeSymbol typeSymbol)
            {
                var syntax = await typeSymbol.DeclaringSyntaxReferences[0].GetSyntaxAsync();
                var inputSemanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
                result.TryAdd(typeSymbol, new MetaType(typeSymbol, syntax as BaseTypeDeclarationSyntax, inputSemanticModel));
            }

            await Task.WhenAll(GetAllTypeSymbolsVisitor.Execute(compilation).Select(TryAdd));
            return result.ToImmutableDictionary(SymbolEqualityComparer.Default);
        }

        private static ImmutableHashSet<INamedTypeSymbol> GetIntrinsicSymbols(CSharpCompilation compilation)
        {
            var types = new[] { typeof(byte),
                typeof(short), typeof(ushort),
                typeof(int), typeof(uint),
                typeof(long), typeof(ulong),
                typeof(decimal), typeof(double), typeof(float),
                typeof(string), typeof(Guid), typeof(Uri),
                typeof(DateTimeOffset)
            };
            var values = types.Select(t => compilation.GetTypeByMetadataName(t.FullName)).ToList();
            return values.ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        }

        public static async Task<RichGenerationResult> GenerateFromInterfaceAsync(InterfaceDeclarationSyntax applyTo, TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            try
            {
                if (AllNamedTypeSymbols == null)
                {
                    AllNamedTypeSymbols = await GetAllTypeDefinitionsAsync(context.Compilation);
                }

                if (IntrinsicSymbols == null)
                {
                    IntrinsicSymbols = GetIntrinsicSymbols(context.Compilation);
                }

                // Ensure code gets generated only once per definition
                var typeSymbol = context.SemanticModel.GetDeclaredSymbol(applyTo);
                if (typeSymbol != null)
                {
                    if (AllNamedTypeSymbols.TryGetValue(typeSymbol, out var metaType))
                    {
                        return new RichGenerationResult() { Members = SyntaxFactory.List(await GenerateAsync(metaType, progress, cancellationToken)) };
                    }
                }
            }
            catch (Exception e)
            {
                progress.Report(Diagnostic.Create(UnexpectedDescriptor, applyTo.GetLocation(), e.Message));

            }
            return new RichGenerationResult();
        }

        private static DiagnosticDescriptor NotSupportedDescriptor => new DiagnosticDescriptor("OCC001",
                    new LocalizableResourceString(nameof(DiagnosticStrings.OCC001), DiagnosticStrings.ResourceManager, typeof(DiagnosticStrings)),
                    new LocalizableResourceString(nameof(DiagnosticStrings.OCC001), DiagnosticStrings.ResourceManager, typeof(DiagnosticStrings)),
                    "Design", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        private static DiagnosticDescriptor UnexpectedDescriptor => new DiagnosticDescriptor("OCC002",
                    new LocalizableResourceString(nameof(DiagnosticStrings.OCC002), DiagnosticStrings.ResourceManager, typeof(DiagnosticStrings)),
                    new LocalizableResourceString(nameof(DiagnosticStrings.OCC002), DiagnosticStrings.ResourceManager, typeof(DiagnosticStrings)),
                    "Design", DiagnosticSeverity.Error, isEnabledByDefault: true);

        private static async Task<ImmutableArray<MemberDeclarationSyntax>> GenerateAsync(MetaType metaType, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
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
                            case TypeKind.Interface:
                                var result = await GenerateDependenciesAsync(metaType, progress, cancellationToken).ConfigureAwait(false);
                                result = result.Add(await CreateClassDeclarationAsync(metaType).ConfigureAwait(false));
                                var descendents = await metaType.GetDirectDescendentsAsync();
                                var tasks = await Task.WhenAll(descendents.Select(to => GenerateAsync(to, progress, cancellationToken))).ConfigureAwait(false);
                                foreach (var r in tasks)
                                {
                                    result = result.AddRange(r);
                                }
                                result = result.Add(await CreateJsonSerializerForinterfaceAsync(metaType));
                                return result;
                            case TypeKind.Enum:
                                return ImmutableArray<MemberDeclarationSyntax>.Empty.Add(CreateEnumSerializerClass(metaType));
                            default:
                                progress.Report(Diagnostic.Create(NotSupportedDescriptor, metaType.DeclarationSyntax.GetLocation(), key.TypeKind, key.Name));
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                progress.Report(Diagnostic.Create(UnexpectedDescriptor, metaType.DeclarationSyntax?.GetLocation() ?? Location.None, e.Message));
            }
            return ImmutableArray<MemberDeclarationSyntax>.Empty;
        }

        private static async Task<ImmutableArray<MemberDeclarationSyntax>> GenerateDependenciesAsync(MetaType metaType, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var allProperties = await metaType.GetLocalPropertiesAsync();
            var dependencies = allProperties.Select(p => p.SafeType).Where(s => !IntrinsicSymbols.Contains(s)).ToImmutableArray();
            var result = ImmutableArray<MemberDeclarationSyntax>.Empty;
            if (dependencies.Length > 0)
            {
                var tasks = AllNamedTypeSymbols.Where(to => dependencies.Contains(to.Key)).Select(to => GenerateAsync(to.Value, progress, cancellationToken));
                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var r in results)
                {
                    result = result.AddRange(r);
                }
            }
            return result;
        }

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
            return fields.Where(f => f.IsRequired).Concat(fields.Where(f => !f.IsRequired));
        }

        private static ParameterListSyntax CreateParameterList(IEnumerable<MetaProperty> properties)
        {
            properties = SortRequiredPropertiesFirst(properties);

            ParameterSyntax SetTypeAndDefault(ParameterSyntax p, MetaProperty f) => f.IsNullable
                 //? p.WithType(f.TypeSyntax).WithDefault(SyntaxFactory.EqualsValueClause(f.Type.IsValueType ? SyntaxFactory.DefaultExpression((f.TypeSyntax)) : (ExpressionSyntax)Syntax.Generator.NullLiteralExpression()))
                 ? p.WithType(f.TypeSyntax).WithDefault(SyntaxFactory.EqualsValueClause((ExpressionSyntax)Syntax.Generator.NullLiteralExpression()))
                 : p.WithType(f.TypeSyntax);

            return SyntaxFactory.ParameterList(
                Syntax.JoinSyntaxNodes(
                    SyntaxKind.CommaToken,
                    properties.Select(f => SetTypeAndDefault(SyntaxFactory.Parameter(f.NameAsArgument.Identifier), f))));
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
        private static ArgumentListSyntax CreateAssignmentList(IEnumerable<MetaProperty> properties)
        {
            return SyntaxFactory.ArgumentList(Syntax.JoinSyntaxNodes(
                           SyntaxKind.CommaToken,
                           properties.Select(f =>
                               SyntaxFactory.Argument(
                                   SyntaxFactory.NameColon(f.NameAsArgument),
                                   NoneToken,
                                   f.NameAsArgument))));
        }
    }
}
