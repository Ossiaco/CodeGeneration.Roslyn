namespace Chorus.CodeGenerator
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal class SupressCompilerWarning : FeatureGenerator
    {
        private readonly int _warning;
        private readonly string _comment;
        public SupressCompilerWarning(CodeGen generator, int warning, string comment) : base(generator)
        {
            _warning = warning;
            _comment = comment;
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
                 .WithLeadingTrivia(
                    SyntaxFactory.ElasticCarriageReturnLineFeed,
                    SyntaxFactory.Trivia(
                        SyntaxFactory.PragmaWarningDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.DisableKeyword), true)
                        .WithErrorCodes(SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(_warning)
                                .WithTrailingTrivia(SyntaxFactory.Space, SyntaxFactory.Comment($"// {_comment}")))))
                        .WithEndOfDirectiveToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.EndOfDirectiveToken, SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed)))))
                .WithTrailingTrivia(
                    SyntaxFactory.Trivia(
                        SyntaxFactory.PragmaWarningDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.RestoreKeyword), true)
                        .WithErrorCodes(SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(_warning))))
                        .WithEndOfDirectiveToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.EndOfDirectiveToken, SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed)))),
                    SyntaxFactory.ElasticCarriageReturnLineFeed);
        }
    }


    internal class NullableType : FeatureGenerator
    {
        public NullableType(CodeGen generator) : base(generator)
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
