// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

namespace CodeGeneration.Roslyn.Generate
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using CodeGeneration.Chorus;
    using CodeGeneration.Roslyn.Tool.CommandLine;
    using Microsoft.CodeAnalysis;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if(args.Length == 1)
            {
                if (File.Exists(args[0]))
                {
                    using var f = File.OpenText(args[0]);
                    args = (await f.ReadToEndAsync()).Split(Environment.NewLine);
                }
            }
            IReadOnlyList<string> compile = Array.Empty<string>();
            IReadOnlyList<string> refs = Array.Empty<string>();
            IReadOnlyList<string> preprocessorSymbols = Array.Empty<string>();
            IReadOnlyList<string> generatorSearchPaths = Array.Empty<string>();
            string generatedCompileItemFile = null;
            string outputDirectory = null;
            string projectDir = null;
            var version = false;
            ArgumentSyntax.Parse(args, syntax =>
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

            if (version)
            {
                Console.WriteLine(ThisAssembly.AssemblyInformationalVersion);
                return 0;
            }
            if (!compile.Any())
            {
                await Console.Error.WriteLineAsync("No source files are specified.");
                return 1;
            }

            if (outputDirectory == null)
            {
                await Console.Error.WriteLineAsync("The output directory must be specified.");
                return 2;
            }

            var generator = new CompilationGenerator
            {
                ProjectDirectory = projectDir,
                Compile = Sanitize(projectDir, compile),
                ReferencePaths = Sanitize(refs),
                PreprocessorSymbols = preprocessorSymbols,
                GeneratorAssemblySearchPaths = Sanitize(generatorSearchPaths),
                IntermediateOutputDirectory = outputDirectory,
            };

            var progress = new Progress<Diagnostic>(OnDiagnosticProgress);

            try
            {
                await generator.GenerateAsync(progress);
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"{e.GetType().Name}: {e.Message}");
                await Console.Error.WriteLineAsync(e.ToString());
                return 3;
            }

            if (generatedCompileItemFile != null)
            {
                File.WriteAllLines(generatedCompileItemFile, generator.GeneratedFiles);
            }

            foreach (var file in generator.GeneratedFiles)
            {
                Logger.Info(file);
            }

            return 0;
        }

        private static void OnDiagnosticProgress(Diagnostic diagnostic)
        {
            Console.WriteLine(diagnostic.ToString());
        }

        private static IReadOnlyList<string> Sanitize(string workingDirectory, IReadOnlyList<string> inputs)
        {
            return inputs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Path.Combine(workingDirectory, x.Trim())).ToArray();
        }

        private static IReadOnlyList<string> Sanitize(IReadOnlyList<string> inputs)
        {
            return inputs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        }
    }
}