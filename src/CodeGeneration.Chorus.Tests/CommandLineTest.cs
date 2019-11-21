namespace CodeGeneration.Chorus.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using CodeGeneration.Roslyn.Engine;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Testing;
    using Xunit;
    using Xunit.Abstractions;

    public class CommandLineTest : LoggedTest
    {

        public CommandLineTest(ITestOutputHelper logger)
        : base(logger)
        {
        }

        [Fact]
        public async Task TestChorusBuildAsync()
        {
            const string responsFile = @"\ossiaco\dotnet\artifacts\obj\Chorus\Debug\netcoreapp3.0\Chorus.csproj.dotnet-codegen.rsp";
            const string workingDirectory = @"\git\ossiaco\Chorus.Azure.Cosmos\src";
            var targetFile = new FileInfo(@"\ossiaco\dotnet\src\Chorus\src\Azure\Cosmos\IResource.cs");
            await ExecuteAsync(responsFile, workingDirectory, targetFile);
        }

        [Fact]
        public async Task TestCosmosBuildAsync()
        {
            const string responsFile = @"\git\ossiaco\Chorus.Azure.Cosmos\src\obj\Debug\netstandard2.0\Chorus.Azure.Cosmos.csproj.dotnet-codegen.rsp";
            const string workingDirectory = @"\git\ossiaco\Chorus.Azure.Cosmos\src";
            var targetFile = new FileInfo(@"\git\ossiaco\Chorus.Azure.Cosmos\src\Azure\Documents\IResource.cs");
            await ExecuteAsync(responsFile, workingDirectory, targetFile);
        }

        private async Task ExecuteAsync(string responsFile, string workingDirectory, FileInfo targetFile)
        {
            var targetFileName = targetFile.FullName;
            DocumentId? targetDocumentId = null;

            Assert.True(File.Exists(responsFile), "The needed response file does not exist");
            Assert.True(Directory.Exists(workingDirectory), "The chorus working directory does not exist");
            using var f = File.OpenText(responsFile);
            var args = (await f.ReadToEndAsync()).Split(Environment.NewLine);

            IReadOnlyList<string> compile = Array.Empty<string>();
            IReadOnlyList<string> refs = Array.Empty<string>();
            IReadOnlyList<string> preprocessorSymbols = Array.Empty<string>();
            IReadOnlyList<string> generatorSearchPaths = Array.Empty<string>();

            var generatedCompileItemFile = string.Empty;
            var outputDirectory = string.Empty;
            var projectDir = string.Empty;
            var version = false;

            Roslyn.Tool.CommandLine.ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineOption("version", ref version, "Show version of this tool (and exits).");
                syntax.DefineOptionList("r|reference", ref refs, "Paths to assemblies being referenced");
                syntax.DefineOptionList("d|define", ref preprocessorSymbols, "Preprocessor symbols");
                syntax.DefineOptionList("generatorSearchPath", ref generatorSearchPaths, "Paths to folders that may contain generator assemblies");
                syntax.DefineOption("out", ref outputDirectory, true, "The directory to write generated source files to");
                syntax.DefineOption("projectDir", ref projectDir, true, "The absolute path of the directory where the project file is located");
                syntax.DefineOption("generatedFilesList", ref generatedCompileItemFile, "The path to the file to create with a list of generated source files");
                syntax.DefineParameterList("compile", ref compile, "Source files included in compilation");
            });

            Assert.False(version);
            Assert.True(compile.Any(), "There are not target files to compile.");
            Assert.NotNull(outputDirectory);

            var compilerOtionOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable);

            var workspace = new AdhocWorkspace();
            var project = workspace.CurrentSolution
                .AddProject("test", "test", "C#")
                .WithCompilationOptions(compilerOtionOptions)
                .WithParseOptions(new CSharpParseOptions(LanguageVersion.Preview))
                .AddMetadataReferences(Sanitize(refs).Select(a => MetadataReference.CreateFromFile(a)));

            var solution = project.Solution;

            foreach (var filename in Sanitize(compile))
            {
                var fi = new FileInfo(Path.Combine(workingDirectory, filename));
                Assert.True(fi.Exists, $"the source file '{fi.FullName}' was not found.");

                var documentId = DocumentId.CreateNewId(project.Id, filename);
                solution = solution.AddDocument(documentId, filename, SourceText.From(fi.OpenRead()));
                if (string.Compare(fi.FullName, targetFileName, true) == 0)
                {
                    targetDocumentId = documentId;
                }
            }

            Assert.NotNull(targetDocumentId);
            using var tw = File.CreateText($"{targetFile.Name}_generated.cs");
            await GenerateAsync(solution.GetDocument(targetDocumentId!)!, Logger, tw);
        }

        private async Task<GenerationResult> GenerateAsync(TextDocument targetDocument, ILogger logger, TextWriter textWriter)
        {
            var generatorDiagnostics = new List<Diagnostic>();
            var progress = new SynchronousProgress<Diagnostic>(generatorDiagnostics.Add);

            var supressions = new List<KeyValuePair<string, ReportDiagnostic>> {
                    new KeyValuePair<string, ReportDiagnostic>("CS1702", ReportDiagnostic.Suppress),
                    new KeyValuePair<string, ReportDiagnostic>("CS8019", ReportDiagnostic.Suppress)
                };

            var inputCompilation = ((CSharpCompilation)(await targetDocument.Project.GetCompilationAsync().ConfigureAwait(false))!)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithAllowUnsafe(true)
                .WithSpecificDiagnosticOptions(supressions)
                .WithNullableContextOptions(NullableContextOptions.Enable));
            var inputSyntaxTree = await (targetDocument.Project.GetDocument(targetDocument.Id)?.GetSyntaxTreeAsync() ?? Task.FromResult(default(SyntaxTree)));

            var outputSyntaxTree = await DocumentTransform.TransformAsync(inputCompilation, inputSyntaxTree, null, progress);
            var outputDocument = targetDocument.Project.AddDocument("output.cs", await outputSyntaxTree.GetRootAsync());

            // Make sure the result compiles without errors or warnings.
            var compilation = ((CSharpCompilation)(await outputDocument.Project.GetCompilationAsync().ConfigureAwait(false))!)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithSpecificDiagnosticOptions(supressions)
                .WithAllowUnsafe(true)
                .WithNullableContextOptions(NullableContextOptions.Enable));

            var compilationDiagnostics = compilation.GetDiagnostics();

            var inputDocumentText = await targetDocument.GetTextAsync();
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

        private static IReadOnlyList<string> Sanitize(IReadOnlyList<string> inputs)
        {
            return inputs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
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

    }
}
