namespace CodeGeneration.Chorus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CodeGeneration.Roslyn;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public class CodeGenerator : IRichCodeGenerator
    {
        private readonly AttributeData _attributeData;

        public CodeGenerator(AttributeData attributeData)
        {
            _attributeData = attributeData;
        }


        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Invalid processing node type");
        }

        public Task<RichGenerationResult> GenerateRichAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            if (context.ProcessingNode is InterfaceDeclarationSyntax source)
            {
                return CodeGen.GenerateAsync(source, context, progress, new CodeGenOptions(_attributeData), cancellationToken);
            }

            throw new ArgumentException("Invalid processing node type");
        }
    }
}