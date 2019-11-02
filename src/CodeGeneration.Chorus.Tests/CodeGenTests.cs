namespace CodeGeneration.Chorus.Tests
{
    using System.Collections.Generic;
    using System.Data.Entity.Design.PluralizationServices;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using CodeGeneration.Roslyn.Engine;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Testing;
    using Xunit;
    using Xunit.Abstractions;


    public class CodeGenTests : LoggedTest
    {
        protected Solution solution;
        protected ProjectId projectId;
        protected DocumentId inputDocumentId;

        public CodeGenTests(ITestOutputHelper logger)
            : base(logger)
        {
            // Requires.NotNull(logger, nameof(logger));


            var workspace = new AdhocWorkspace();
            var project = workspace.CurrentSolution.AddProject("test", "test", "C#")
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable))
                .WithParseOptions(new CSharpParseOptions(LanguageVersion.Preview))
                .AddMetadataReferences(GetNetCoreAppReferences())
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(JsonElement).Assembly.Location))
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(GenerateClassAttribute).Assembly.Location))
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(PluralizationService).Assembly.Location))
                .AddMetadataReference(MetadataReference.CreateFromFile(@"\ossiaco\dotnet\artifacts\bin\Chorus\Debug\netcoreapp3.0\chorus.dll"));
            var inputDocument = project.AddDocument("input.cs", string.Empty);
            inputDocumentId = inputDocument.Id;
            project = inputDocument.Project;
            projectId = inputDocument.Project.Id;
            solution = project.Solution;
        }

        [Fact]
        public async Task SimpleCompileTestAsync()
        {
            using var tw = File.CreateText($"SimpleCompileTest.cs");
            await GenerateAsync(SourceText.From("public class Foo{}"), Logger, tw);
        }

        [Fact]
        public async Task SimpleTypeTestAsync()
        {
            using var tw = File.CreateText($"SimpleTypeTest.cs");
            await GenerateFromStreamAsync("IResource", Logger, tw);
        }
        [Fact]
        public async Task SimpleResourceTestAsync()
        {
            using var tw = File.CreateText($"SimpleResourceTest.cs");
            await GenerateFromStreamAsync("IResource2", Logger, tw);
        }

        [Fact]
        public async Task DerivedResourceTestAsync()
        {
            using var tw = File.CreateText($"DerivedResourceTest.cs");
            await GenerateFromStreamAsync("IResource3", Logger, tw);
        }


        [Fact]
        public async Task IntrinsicTypesTestAsync()
        {
            var intrinsicTypes = new[] {
                "IBooleanType",
                "IByteType",
                "IDateTimeOffsetType",
                "IDecimalType",
                "IDoubleType",
                "IGuidType",
                "IInt16Type",
                "IInt32Type",
                "IInt64Type",
                "ISingleType",
                "IStringType",
                "IUint16Type",
                "IUint32Type",
                "IUint64Type",
                "IUriType"
                };

            foreach (var resource in intrinsicTypes)
            {
                using var tw = File.CreateText($"{resource}.cs");
                Logger.LogInformation($"Generating {resource}");
                await GenerateFromStreamAsync(resource, Logger, tw);
            }
        }


        private async Task<GenerationResult> GenerateFromStreamAsync(string testName, ILogger logger, TextWriter textWriter)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType().Namespace + ".TestSources." + testName + ".cs"))
            {
                var result = await GenerateAsync(SourceText.From(stream), logger, textWriter);
                var classes = result.Declarations.Where(d => d.DeclaredNode.IsKind(SyntaxKind.ClassDeclaration));
                Assert.Empty(result.CompilationDiagnostics.Where(d => !d.IsSuppressed && d.Severity != DiagnosticSeverity.Hidden));

                return result;
            }
        }

        private async Task<GenerationResult> GenerateAsync(SourceText inputSource, ILogger logger, TextWriter textWriter)
        {
            var solution = this.solution.WithDocumentText(inputDocumentId, inputSource);
            var inputDocument = solution.GetDocument(inputDocumentId)!;
            var generatorDiagnostics = new List<Diagnostic>();
            var progress = new SynchronousProgress<Diagnostic>(generatorDiagnostics.Add);

            var inputCompilation = ((CSharpCompilation)(await inputDocument.Project.GetCompilationAsync().ConfigureAwait(false))!)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
            var inputSyntaxTree = await inputDocument.GetSyntaxTreeAsync();

            var outputSyntaxTree = await DocumentTransform.TransformAsync(inputCompilation, inputSyntaxTree, null, progress);
            var outputDocument = inputDocument.Project.AddDocument("output.cs", await outputSyntaxTree.GetRootAsync());

            // Make sure the result compiles without errors or warnings.
            var compilation = ((CSharpCompilation)(await outputDocument.Project.GetCompilationAsync().ConfigureAwait(false))!)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

            var compilationDiagnostics = compilation.GetDiagnostics();

            var inputDocumentText = await inputDocument.GetTextAsync();
            var outputDocumentText = await outputDocument.GetTextAsync();

            await textWriter.WriteLineAsync(outputDocumentText.ToString());
            await textWriter.WriteLineAsync(string.Empty);
            await textWriter.WriteLineAsync(string.Empty);

            // Verify all line endings are consistent(otherwise VS can bug the heck out of the user if they have the generated file open).
            string? firstLineEnding = null;
            foreach (var line in outputDocumentText.Lines)
            {
                var actualNewLine = line.Text.GetSubText(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak)).ToString();
                if (firstLineEnding == null)
                {
                    firstLineEnding = actualNewLine;
                }
                else if (actualNewLine != firstLineEnding && actualNewLine.Length > 0)
                {
                    var expected = EscapeLineEndingCharacters(firstLineEnding);
                    var actual = EscapeLineEndingCharacters(actualNewLine);
                    Assert.True(false, $"Expected line ending characters '{expected}' but found '{actual}' on line {line.LineNumber + 1}.\nContent: {line}");
                }
            }

            var semanticModel = await outputDocument.GetSemanticModelAsync();
            var result = new GenerationResult(outputDocument, semanticModel!, generatorDiagnostics, compilationDiagnostics);
            await textWriter.WriteLineAsync($"// GeneratorDiagnostics:");
            logger.LogInformation($"GeneratorDiagnostics:");
            await textWriter.WriteLineAsync(string.Empty);
            foreach (var diagnostic in generatorDiagnostics)
            {
                await textWriter.WriteLineAsync($"// {diagnostic.ToString()}");
                logger.LogError(diagnostic.ToString());
            }
            await textWriter.WriteLineAsync(string.Empty);
            await textWriter.WriteLineAsync(string.Empty);

            await textWriter.WriteLineAsync($"// CompilationDiagnostics:");
            logger.LogInformation($"CompilationDiagnostics:");
            await textWriter.WriteLineAsync(string.Empty);
            foreach (var diagnostic in compilationDiagnostics)
            {
                await textWriter.WriteLineAsync($"// {diagnostic.ToString()}");
                logger.LogError(diagnostic.ToString());
            }
            return result;
        }

        private static string EscapeLineEndingCharacters(string whitespace)
        {
            var builder = new StringBuilder(whitespace.Length * 2);
            foreach (var ch in whitespace)
            {
                switch (ch)
                {
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    default:
                        builder.Append(ch);
                        break;
                }
            }

            return builder.ToString();
        }

        private static IEnumerable<MetadataReference> GetNetCoreAppReferences()
        {
            var profileDirectory = @"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.0.0\ref\netcoreapp3.0";
            foreach (var assembly in Directory.GetFiles(profileDirectory, "*.dll"))
            {
                yield return MetadataReference.CreateFromFile(assembly);
            }
        }

        private static IEnumerable<MetadataReference> GetAllReferences(Assembly assembly)
        {
            var dir = new FileInfo(assembly.Location).Directory;
            foreach (var fi in dir.GetFiles("*.dll"))
            {
                yield return MetadataReference.CreateFromFile(fi.FullName);
            }
        }
    }
}
