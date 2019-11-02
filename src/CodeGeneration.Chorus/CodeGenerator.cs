//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using CodeGeneration.Roslyn;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="CodeGenerator" />
    /// </summary>
    public class CodeGenerator : IRichCodeGenerator
    {
        /// <summary>
        /// Defines the _attributeData
        /// </summary>
        private readonly AttributeData _attributeData;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeGenerator"/> class.
        /// </summary>
        /// <param name="attributeData">The attributeData<see cref="AttributeData"/></param>
        public CodeGenerator(AttributeData attributeData)
        {
            _attributeData = attributeData;
        }

        /// <summary>
        /// The GenerateAsync
        /// </summary>
        /// <param name="context">The context<see cref="TransformationContext"/></param>
        /// <param name="progress">The progress<see cref="IProgress{Diagnostic}"/></param>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        /// <returns>The <see cref="T:Task{SyntaxList{MemberDeclarationSyntax}}"/></returns>
        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Invalid processing node type");
        }

        /// <summary>
        /// The GenerateRichAsync
        /// </summary>
        /// <param name="context">The context<see cref="TransformationContext"/></param>
        /// <param name="progress">The progress<see cref="IProgress{Diagnostic}"/></param>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        /// <returns>The <see cref="Task{RichGenerationResult}"/></returns>
        public Task<RichGenerationResult> GenerateRichAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            if (context.ProcessingNode is InterfaceDeclarationSyntax source)
            {
                return CodeGen.GenerateFromInterfaceAsync(source, context, progress, cancellationToken);
            }

            throw new ArgumentException("Invalid processing node type");
        }
    }
}
