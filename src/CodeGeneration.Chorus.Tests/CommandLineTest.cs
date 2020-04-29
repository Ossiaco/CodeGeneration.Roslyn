namespace CodeGeneration.Chorus.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using CodeGeneration.Chorus;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;
    using Microsoft.Extensions.Logging;
    using Xunit;
    using Xunit.Abstractions;

    public class CommandLineTest
    {
        private readonly ILoggerFactory loggerFactory;

        public CommandLineTest(ITestOutputHelper outputHelper)
        {
            this.loggerFactory = Divergic.Logging.Xunit.LogFactory.Create(outputHelper);
        }

        [Fact]
        public async Task TestChorusBuild()
        {
            var logger = this.loggerFactory.CreateLogger<CommandLineTest>();
            try
            {
                const string responsFile = @"\ossiaco\chorus\artifacts\obj\Chorus\Debug\netstandard2.1\Chorus.csproj.dotnet-codegen.rsp";
                const string workingDirectory = @"\ossiaco\\chorus\src\Chorus\src";
                await ExecuteAsync(responsFile, workingDirectory, logger);
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
            }
        }

        private async Task ExecuteAsync(string responsFile, string workingDirectory, ILogger logger)
        {
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


            compile = Sanitize(workingDirectory, compile);
            var generator = new CompilationGenerator
            {
                ProjectDirectory = workingDirectory,
                Compile = compile,
                ReferencePaths = Sanitize(refs),
                PreprocessorSymbols = preprocessorSymbols,
                GeneratorAssemblySearchPaths = Sanitize(generatorSearchPaths),
                IntermediateOutputDirectory = Path.Combine(workingDirectory, outputDirectory),
            };

            void OnDiagnosticProgress(Diagnostic diagnostic)
            {
                logger.LogInformation(diagnostic.ToString());
            }

            var progress = new Progress<Diagnostic>(OnDiagnosticProgress);

            foreach (var filename in compile)
            {
                Assert.True(File.Exists(filename), $"the source file '{filename}' was not found.");
                // var documentId = DocumentId.CreateNewId(project.Id, filename);
                // solution = solution.AddDocument(documentId, filename, SourceText.From(fi.OpenRead()));
            }

            try
            {
                await generator.GenerateAsync(progress);
            }
            catch (Exception e)
            {
                logger.LogError($"{e.GetType().Name}: {e.Message}");
                logger.LogError(e.ToString());
                return;
            }

            if (generatedCompileItemFile != null)
            {
                File.WriteAllLines(Path.Combine(workingDirectory, generatedCompileItemFile), generator.GeneratedFiles);
            }

            Validate(generator, logger);

        }
        private CSharpCompilation CreateCompilation(CompilationGenerator genrator)
        {
            var supressions = new List<KeyValuePair<string, ReportDiagnostic>> {
                    new KeyValuePair<string, ReportDiagnostic>("CS1702", ReportDiagnostic.Suppress),
                    new KeyValuePair<string, ReportDiagnostic>("CS8019", ReportDiagnostic.Suppress)
                };


            var compilation = CSharpCompilation.Create("codegen")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithAllowUnsafe(true)
                    .WithSpecificDiagnosticOptions(supressions)
                    .WithNullableContextOptions(NullableContextOptions.Enable))
                .WithReferences(genrator.ReferencePaths.Select(p => MetadataReference.CreateFromFile(p)));

            var parseOptions = new CSharpParseOptions(preprocessorSymbols: genrator.PreprocessorSymbols);

            void AddSyntaxTrees(string sourceFile)
            {
                using (var stream = File.OpenRead(sourceFile))
                {
                    var text = SourceText.From(stream);
                    compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(text, parseOptions, sourceFile));
                }
            }
            foreach (var sourceFile in genrator.Compile)
            {
                AddSyntaxTrees(sourceFile);
            }
            foreach (var sourceFile in genrator.GeneratedFiles.Where(s => File.Exists(s)))
            {
                AddSyntaxTrees(sourceFile);
            }

            return compilation;
        }


        private void Validate(CompilationGenerator genrator, ILogger logger)
        {
            var generatorDiagnostics = new List<Diagnostic>();
            var progress = new SynchronousProgress<Diagnostic>(generatorDiagnostics.Add);

            var compilation = CreateCompilation(genrator);
            var compilationDiagnostics = compilation.GetDiagnostics();

            logger.LogInformation($"GeneratorDiagnostics:");
            foreach (var diagnostic in generatorDiagnostics)
            {
                LogDiagnostic(diagnostic, logger);
            }
            logger.LogInformation($"CompilationDiagnostics:");
            foreach (var diagnostic in compilationDiagnostics)
            {
                LogDiagnostic(diagnostic, logger);
            }
        }

        private void LogDiagnostic(Diagnostic diagnostic, ILogger logger)
        {
            switch (diagnostic.Severity)
            {
                case DiagnosticSeverity.Warning:
                    logger.LogWarning(diagnostic.ToString());
                    break;
                case DiagnosticSeverity.Info:
                    logger.LogInformation(diagnostic.ToString());
                    break;
                case DiagnosticSeverity.Error:
                    logger.LogError(diagnostic.ToString());
                    break;
            }
        }

        private static IReadOnlyList<string> Sanitize(string workingDirectory, IReadOnlyList<string> inputs)
        {
            return inputs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Path.Combine(workingDirectory, x.Trim())).ToArray();
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
