namespace CodeGeneration.Chorus.Tests
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.PooledObjects;
    using Microsoft.CodeAnalysis.Text;

    internal class GenerationResult
    {
        public GenerationResult(
            Document document,
            SemanticModel semanticModel,
            IReadOnlyList<Diagnostic> generatorDiagnostics,
            IReadOnlyList<Diagnostic> compilationDiagnostics)
        {
            Document = document;
            SemanticModel = semanticModel;
            var declarationInfoBuilder = ArrayBuilder<DeclarationInfo>.GetInstance();
            CSharpDeclarationComputer.ComputeDeclarationsInSpan(semanticModel, TextSpan.FromBounds(0, semanticModel.SyntaxTree.Length), true, declarationInfoBuilder, CancellationToken.None);
            Declarations = declarationInfoBuilder.ToImmutableAndFree();
            GeneratorDiagnostics = generatorDiagnostics;
            CompilationDiagnostics = compilationDiagnostics;
        }

        public Document Document { get; private set; }

        public SemanticModel SemanticModel { get; private set; }

        internal ImmutableArray<DeclarationInfo> Declarations { get; private set; }

        public IEnumerable<ISymbol> DeclaredSymbols
        {
            get { return Declarations.Select(d => d.DeclaredSymbol); }
        }

        public IEnumerable<IMethodSymbol> DeclaredMethods
        {
            get { return DeclaredSymbols.OfType<IMethodSymbol>(); }
        }

        public IEnumerable<IPropertySymbol> DeclaredProperties
        {
            get { return DeclaredSymbols.OfType<IPropertySymbol>(); }
        }

        public IEnumerable<INamedTypeSymbol> DeclaredTypes
        {
            get { return DeclaredSymbols.OfType<INamedTypeSymbol>(); }
        }

        public IReadOnlyList<Diagnostic> GeneratorDiagnostics { get; }

        public IReadOnlyList<Diagnostic> CompilationDiagnostics { get; }
    }

}
