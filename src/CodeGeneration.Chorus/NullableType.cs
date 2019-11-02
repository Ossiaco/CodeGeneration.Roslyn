namespace CodeGeneration.Chorus
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal class NullableType : FeatureGenerator
    {
        public NullableType(MetaType applyTo) : base(applyTo)
        {
        }

        public override bool IsApplicable
        {
            get { return true; }
        }

        protected override void GenerateCore()
        {
        }

        public override ClassDeclarationSyntax ProcessApplyToClassDeclaration(ClassDeclarationSyntax applyTo)
        {
            return applyTo
                .WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed, SyntaxFactory.Trivia(SyntaxFactory.NullableDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.EnableKeyword), true)))
                .WithTrailingTrivia(SyntaxFactory.Trivia(SyntaxFactory.NullableDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.RestoreKeyword), true)), SyntaxFactory.ElasticCarriageReturnLineFeed);
        }
    }


}
