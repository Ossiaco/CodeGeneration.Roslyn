﻿//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using CodeGeneration.Chorus.Json;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal static class DocumentTransform
    {
        internal static readonly string GeneratedByAToolPreamble = @"// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Date: {0:R}
//     Version: {1}
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------
".Replace("\r\n", "\n").Replace("\n", Environment.NewLine);// normalize regardless of git checkout policy

        internal static async Task<(SyntaxTree, bool)> TransformAsync(ITransformationContext transformationContext, IEnumerable<MetaType> metaTypes)
        {
            Requires.NotNull(transformationContext, nameof(transformationContext));
            Requires.NotNull(transformationContext.Compilation, nameof(transformationContext.Compilation));
            Requires.NotNull(metaTypes, nameof(metaTypes));

            var inputDocument = metaTypes.First().DeclarationSyntax.SyntaxTree;
            var compilation = transformationContext.Compilation;
            var inputSemanticModel = compilation.GetSemanticModel(inputDocument);
            var inputCompilationUnit = inputDocument.GetCompilationUnitRoot();

            var emittedExterns = inputCompilationUnit
                .Externs
                .Select(x => x.WithoutTrivia())
                .ToImmutableArray();

            var emittedUsings = inputCompilationUnit
                .Usings
                .Select(x => x.WithoutTrivia())
                .ToImmutableArray();

            var emittedAttributeLists = ImmutableArray<AttributeListSyntax>.Empty;
            var emittedMembers = ImmutableArray<MemberDeclarationSyntax>.Empty;

            var version = typeof(DocumentTransform).Assembly.GetName().Version.ToString();

            foreach (var metaType in metaTypes)
            {
                var codegen = new CodeGen(metaType, transformationContext);
                var members = await codegen.GenerateAsync();
                emittedMembers = emittedMembers.AddRange(members);

                //emittedExterns = emittedExterns.AddRange(emitted.Externs);
                //emittedUsings = emittedUsings.AddRange(emitted.Usings);
                //emittedAttributeLists = emittedAttributeLists.AddRange(emitted.AttributeLists);
                //emittedMembers = emittedMembers.AddRange(emitted.Members);

            }

            if (emittedMembers.Length > 0)
            {
                var compilationUnit =
                    SyntaxFactory.CompilationUnit(
                            SyntaxFactory.List(emittedExterns),
                            SyntaxFactory.List(emittedUsings),
                            SyntaxFactory.List(emittedAttributeLists),
                            SyntaxFactory.List(emittedMembers))
                        .WithLeadingTrivia(
                                SyntaxFactory.Comment(string.Format(GeneratedByAToolPreamble, DateTimeOffset.UtcNow, version)),
                                SyntaxFactory.ElasticCarriageReturnLineFeed,
                                SyntaxFactory.Trivia(SyntaxFactory.NullableDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.EnableKeyword), true)),
                                SyntaxFactory.ElasticCarriageReturnLineFeed,
                                SyntaxFactory.Trivia(GeneratePragmaWarningDirectiveTrivia("8019")))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                        .NormalizeWhitespace();

                return (compilationUnit.SyntaxTree, true);
            }
            return (default, false);
        }

        private static PragmaWarningDirectiveTriviaSyntax GeneratePragmaWarningDirectiveTrivia(params string[] args)
            => SyntaxFactory.PragmaWarningDirectiveTrivia(
                SyntaxFactory.Token(SyntaxKind.HashToken),
                SyntaxFactory.Token(SyntaxKind.PragmaKeyword),
                SyntaxFactory.Token(SyntaxKind.WarningKeyword),
                SyntaxFactory.Token(SyntaxKind.DisableKeyword),
                SyntaxFactory.SeparatedList<ExpressionSyntax>().AddRange(args.Select(a => SyntaxFactory.IdentifierName(a))),
                SyntaxFactory.Token(SyntaxKind.EndOfDirectiveToken),
                default);

        private static ImmutableArray<AttributeData> GetAttributeData(Compilation compilation, SemanticModel document, SyntaxNode syntaxNode)
        {
            Requires.NotNull(compilation, nameof(compilation));
            Requires.NotNull(document, nameof(document));
            Requires.NotNull(syntaxNode, nameof(syntaxNode));

            switch (syntaxNode)
            {
                case CompilationUnitSyntax syntax:
                    return compilation.Assembly.GetAttributes().Where(x => x.ApplicationSyntaxReference.SyntaxTree == syntax.SyntaxTree).ToImmutableArray();
                default:
                    return document.GetDeclaredSymbol(syntaxNode)?.GetAttributes() ?? ImmutableArray<AttributeData>.Empty;
            }
        }
    }
}
