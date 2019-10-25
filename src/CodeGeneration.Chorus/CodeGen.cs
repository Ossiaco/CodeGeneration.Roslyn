namespace Chorus.CodeGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Data.Entity.Design.PluralizationServices;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CodeGeneration.Roslyn;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;

    public partial class CodeGen
    {
        private static readonly IdentifierNameSyntax DefaultInstanceFieldName = SyntaxFactory.IdentifierName("DefaultInstance");
        private static readonly SyntaxToken NoneToken = SyntaxFactory.Token(SyntaxKind.None);
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

        private readonly InterfaceDeclarationSyntax _interfaceDeclaration;
        internal SemanticModel SemanticModel { get; }
        private readonly IProgress<Diagnostic> _progress;
        private readonly CodeGenOptions _options;
        private readonly CancellationToken _cancellationToken;

        private ImmutableArray<DeclarationInfo> inputDeclarations;
        internal MetaType InterfaceMetaType { get; private set; }
        private readonly TransformationContext _context;
        private IdentifierNameSyntax _applyToTypeName;

        private CodeGen(InterfaceDeclarationSyntax applyFrom, TransformationContext context, IProgress<Diagnostic> progress, CodeGenOptions options, CancellationToken cancellationToken)
        {
            ////Requires.NotNull(applyTo, nameof(applyTo));
            ////Requires.NotNull(semanticModel, nameof(semanticModel));
            ////Requires.NotNull(progress, nameof(progress));

            _context = context;
            _interfaceDeclaration = applyFrom;
            SemanticModel = context.SemanticModel;
            _progress = progress;
            _options = options ?? new CodeGenOptions();
            _cancellationToken = cancellationToken;

            PluralService = PluralizationService.CreateService(new CultureInfo("en-US"));
        }

        public PluralizationService PluralService { get; set; }


        public static async Task<RichGenerationResult> GenerateAsync(InterfaceDeclarationSyntax applyTo, TransformationContext context, IProgress<Diagnostic> progress, CodeGenOptions options, CancellationToken cancellationToken)
        {
            ////Requires.NotNull(applyTo, nameof(applyTo));
            ////Requires.NotNull(semanticModel, nameof(semanticModel));
            ////Requires.NotNull(progress, nameof(progress));

            // Ensure code gets generated only once per definition
            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(applyTo);
            if (typeSymbol != null)
            {
                var key = typeSymbol.OriginalDefinition ?? typeSymbol;
                if (TryAdd(ref CheckedTypes, key))
                {
                    var instance = new CodeGen(applyTo, context, progress, options, cancellationToken);
                    return await instance.GenerateAsync();
                }
            }
            return new RichGenerationResult();
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

        private Task<RichGenerationResult> GenerateAsync()
        {
            _applyToTypeName = _interfaceDeclaration.ClassName();

            inputDeclarations = SemanticModel.ComputeDeclarationsInSpan(TextSpan.FromBounds(0, SemanticModel.SyntaxTree.Length), true, _cancellationToken);
            var applyFromSymbol = SemanticModel.GetDeclaredSymbol(_interfaceDeclaration, _cancellationToken);
            InterfaceMetaType = new MetaType(this, applyFromSymbol);

            var usings = SyntaxFactory.List<UsingDirectiveSyntax>();

            var members = CreateClassDeclaration();
            return Task.FromResult(new RichGenerationResult() { Members = SyntaxFactory.List(members), Usings = usings });
        }


        private ClassDeclarationSyntax CreateOrleansSerializer()
        {
            var className = SyntaxFactory.Identifier($"{_applyToTypeName.Identifier.Text}OrleansSerializer");
            var partialClass = SyntaxFactory.ClassDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            return partialClass;
        }

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


        internal IEnumerable<INamedTypeSymbol> TypesInInputDocument
        {
            get
            {
                return from declaration in inputDeclarations
                       let typeSymbol = declaration.DeclaredSymbol as INamedTypeSymbol
                       where typeSymbol != null
                       select typeSymbol;
            }
        }

        private static IEnumerable<MetaProperty> SortRequiredPropertiesFirst(IEnumerable<MetaProperty> fields)
        {
            return fields.Where(f => f.IsRequired).Concat(fields.Where(f => !f.IsRequired));
        }

        private ParameterListSyntax CreateParameterList(IEnumerable<MetaProperty> fields)
        {
            fields = SortRequiredPropertiesFirst(fields);

            ParameterSyntax SetTypeAndDefault(ParameterSyntax p, MetaProperty f) => f.IsNullable
                 ? p.WithType(f.TypeSyntax).WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.DefaultExpression(f.TypeSyntax)))
                 : p.WithType(f.TypeSyntax);

            return SyntaxFactory.ParameterList(
                Syntax.JoinSyntaxNodes(
                    SyntaxKind.CommaToken,
                    fields.Select(f => SetTypeAndDefault(SyntaxFactory.Parameter(f.NameAsArgument.Identifier), f))));
        }

        private ArgumentListSyntax CreateArgumentList(IEnumerable<MetaProperty> properties)
        {
            return SyntaxFactory.ArgumentList(Syntax.JoinSyntaxNodes(
                           SyntaxKind.CommaToken,
                           properties.Select(f =>
                               SyntaxFactory.Argument(
                                   SyntaxFactory.NameColon(f.NameAsArgument),
                                   NoneToken,
                                   Syntax.ThisDot(f.NameAsProperty)))));
        }
        private ArgumentListSyntax CreateAssignmentList(IEnumerable<MetaProperty> properties)
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
