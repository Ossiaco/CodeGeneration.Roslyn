//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using Microsoft.Extensions.DependencyModel;
    using Microsoft.Extensions.DependencyModel.Resolution;
    using Microsoft.Extensions.Logging;
    using Validation;

    internal interface ITransformationContext
    {
        ImmutableDictionary<INamedTypeSymbol, MetaType> AllNamedTypeSymbols { get; }

        CSharpCompilation Compilation { get; }

        string IntermediateOutputDirectory { get; }

        ImmutableHashSet<INamedTypeSymbol> IntrinsicSymbols { get; }

        INamedTypeSymbol JsonSerializeableType { get; }

        INamedTypeSymbol MessageType { get; }

        IProgress<Diagnostic> Progress { get; }

        INamedTypeSymbol ResponseMessageType { get; }

        int RootLength { get; }

        INamedTypeSymbol VertexType { get; }
    }

    /// <summary>
    /// Defines the <see cref="CompilationGenerator" />
    /// </summary>
    public class CompilationGenerator : ITransformationContext
    {
        private const string InputAssembliesIntermediateOutputFileName = "CodeGeneration.Chorus.InputAssemblies.txt";

        private const int ProcessCannotAccessFileHR = unchecked((int)0x80070020);

        private static readonly HashSet<string> AllowedAssemblyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".dll" };
        private readonly List<string> additionalWrittenFiles = new List<string>();
        private readonly Dictionary<string, Assembly> assembliesByPath = new Dictionary<string, Assembly>();
        private readonly HashSet<string> directoriesWithResolver = new HashSet<string>();
        private readonly List<string> loadedAssemblies = new List<string>();
        private ImmutableDictionary<INamedTypeSymbol, MetaType> allNamedTypeSymbols;
        private CompositeCompilationAssemblyResolver assemblyResolver;
        private CSharpCompilation compilation;
        private DependencyContext dependencyContext;
        private ImmutableHashSet<string> generatedFiles = ImmutableHashSet<string>.Empty;
        private ImmutableHashSet<INamedTypeSymbol> intrinsicSymbols;
        private INamedTypeSymbol jsonSerializeableType;
        private INamedTypeSymbol messageType;
        private IProgress<Diagnostic> progress;
        private INamedTypeSymbol responseMessageType;
        private int rootLength;
        private INamedTypeSymbol vertexType;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompilationGenerator"/> class.
        /// </summary>
        public CompilationGenerator()
        {
            assemblyResolver = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[]
            {
                new ReferenceAssemblyPathResolver(),
                new PackageCompilationAssemblyResolver(),
            });
            dependencyContext = DependencyContext.Default;

            var loadContext = AssemblyLoadContext.GetLoadContext(GetType().GetTypeInfo().Assembly);
            loadContext.Resolving += ResolveAssembly;
        }

        ImmutableDictionary<INamedTypeSymbol, MetaType> ITransformationContext.AllNamedTypeSymbols => this.allNamedTypeSymbols;

        CSharpCompilation ITransformationContext.Compilation => this.compilation;

        string ITransformationContext.IntermediateOutputDirectory => this.IntermediateOutputDirectory;

        ImmutableHashSet<INamedTypeSymbol> ITransformationContext.IntrinsicSymbols => this.intrinsicSymbols;

        INamedTypeSymbol ITransformationContext.JsonSerializeableType => this.jsonSerializeableType;

        INamedTypeSymbol ITransformationContext.MessageType => this.messageType;

        IProgress<Diagnostic> ITransformationContext.Progress => this.progress;

        INamedTypeSymbol ITransformationContext.ResponseMessageType => this.responseMessageType;

        int ITransformationContext.RootLength => this.rootLength;

        INamedTypeSymbol ITransformationContext.VertexType => this.vertexType;

        /// <summary>
        /// Gets the AdditionalWrittenFiles
        /// </summary>
        public IEnumerable<string> AdditionalWrittenFiles => additionalWrittenFiles;

        /// <summary>
        /// Gets or sets the Compile
        /// </summary>
        public IReadOnlyList<string> Compile { get; set; }

        /// <summary>
        /// Gets the FormatStrings
        /// </summary>
        public ImmutableDictionary<INamedTypeSymbol, string> FormatStrings { get; private set; }

        /// <summary>
        /// Gets the GeneratedFiles
        /// </summary>
        public ImmutableHashSet<string> GeneratedFiles => generatedFiles;

        /// <summary>
        /// Gets or sets the GeneratorAssemblySearchPaths
        /// </summary>
        public IReadOnlyList<string> GeneratorAssemblySearchPaths { get; set; }

        /// <summary>
        /// Gets or sets the IntermediateOutputDirectory
        /// </summary>
        public string IntermediateOutputDirectory { get; set; }

        /// <summary>
        /// Gets or sets the PreprocessorSymbols
        /// </summary>
        public IEnumerable<string> PreprocessorSymbols { get; set; }

        /// <summary>
        /// Gets or sets the ProjectDirectory
        /// </summary>
        public string ProjectDirectory { get; set; }

        /// <summary>
        /// Gets or sets the ReferencePath
        /// </summary>
        public IReadOnlyList<string> ReferencePaths { get; set; }

        /// <summary>
        /// The GenerateAsync
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="progress">The progress<see cref="IProgress{Diagnostic}"/></param>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/></param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task GenerateAsync(IProgress<Diagnostic> progress = null, CancellationToken cancellationToken = default)
        {
            Verify.Operation(Compile != null, $"{nameof(Compile)} must be set first.");
            Verify.Operation(ReferencePaths != null, $"{nameof(ReferencePaths)} must be set first.");
            Verify.Operation(IntermediateOutputDirectory != null, $"{nameof(IntermediateOutputDirectory)} must be set first.");
            Verify.Operation(GeneratorAssemblySearchPaths != null, $"{nameof(GeneratorAssemblySearchPaths)} must be set first.");

            this.progress = progress ?? new NullProgress<Diagnostic>();
            compilation = CreateCompilation(cancellationToken);
            jsonSerializeableType = compilation.GetTypeByMetadataName(typeof(IJsonSerialize).FullName);
            responseMessageType = compilation.GetTypeByMetadataName("Chorus.Messaging.IResponseMessage");
            messageType = compilation.GetTypeByMetadataName("Chorus.Messaging.IMessage");
            vertexType = compilation.GetTypeByMetadataName("Chorus.Azure.Cosmos.IVertex");
            intrinsicSymbols = GetIntrinsicSymbols(compilation);

            var generatedFiles = this.generatedFiles.ToBuilder();

            var generatorAssemblyInputsFile = Path.Combine(IntermediateOutputDirectory, InputAssembliesIntermediateOutputFileName);

            // For incremental build, we want to consider the input->output files as well as the assemblies involved in code generation.
            var assembliesLastModified = GetLastModifiedAssemblyTime(generatorAssemblyInputsFile);

            var fileFailures = new List<Exception>();
            rootLength = ProjectDirectory.Length + 1;
            allNamedTypeSymbols = await GetAllTypeDefinitionsAsync(compilation);
            var files = allNamedTypeSymbols.Values.GroupBy(t => t.OutputFilePath).Where(t => t.Key != null).ToImmutableDictionary(n => n.Key, n => n.ToImmutableList());

            var directories = files.Keys.Select(v => Path.GetDirectoryName(v)).ToImmutableHashSet();
            foreach (var directorName in directories)
            {
                if (!Directory.Exists(directorName))
                {
                    Directory.CreateDirectory(directorName);
                }
            }

            foreach (var kp in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (await TransformFileAsync(kp.Key, kp.Value, assembliesLastModified, cancellationToken).ConfigureAwait(false))
                    {
                        generatedFiles.Add(kp.Key);
                    }
                }
                catch (Exception ex)
                {
                    ReportError(this.progress, "CGR001", kp.Value.First().DeclarationSyntax.SyntaxTree, ex);
                    fileFailures.Add(ex);
                    break;
                }
            }

            SaveGeneratorAssemblyList(generatorAssemblyInputsFile);

            if (fileFailures.Count > 0)
            {
                throw new AggregateException(fileFailures);
            }

            this.generatedFiles = generatedFiles.ToImmutable();

        }

        private static RuntimeLibrary FindMatchingLibrary(IEnumerable<RuntimeLibrary> libraries, AssemblyName name)
        {
            foreach (var runtime in libraries)
            {
                if (string.Equals(runtime.Name, name.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }

                // If the NuGet package name does not exactly match the AssemblyName,
                // we check whether the assembly file name is matching
                if (runtime.RuntimeAssemblyGroups.Any(
                        g => g.AssetPaths.Any(
                            p => string.Equals(Path.GetFileNameWithoutExtension(p), name.Name, StringComparison.OrdinalIgnoreCase))))
                {
                    return runtime;
                }
            }
            return null;
        }

        private static ImmutableHashSet<INamedTypeSymbol> GetIntrinsicSymbols(CSharpCompilation compilation)
        {
            var types = new[] { typeof(byte),
                typeof(short), typeof(ushort),
                typeof(int), typeof(uint),
                typeof(long), typeof(ulong),
                typeof(decimal), typeof(double), typeof(float),
                typeof(string), typeof(Guid), typeof(Uri),
                typeof(DateTimeOffset)
            };
            var values = types.Select(t => compilation.GetTypeByMetadataName(t.FullName)).ToList();
            return values.ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        }

        private static DateTime GetLastModifiedAssemblyTime(string assemblyListPath)
        {
            if (!File.Exists(assemblyListPath))
            {
                return DateTime.MinValue;
            }

            var timestamps = (from path in File.ReadAllLines(assemblyListPath)
                              where File.Exists(path)
                              select File.GetLastWriteTime(path)).ToList();
            return timestamps.Any() ? timestamps.Max() : DateTime.MinValue;
        }

        private static void ReportError(IProgress<Diagnostic> progress, string id, SyntaxTree inputSyntaxTree, Exception ex)
        {
            Console.Error.WriteLine($"Exception in file processing: {ex}");

            if (progress == null)
            {
                return;
            }

            const string category = "CodeGen.Roslyn: Transformation";
            const string messageFormat = "{0}";

            var descriptor = new DiagnosticDescriptor(
                id,
                "Error during transformation",
                messageFormat,
                category,
                DiagnosticSeverity.Error,
                true);

            var location = inputSyntaxTree != null ? Location.Create(inputSyntaxTree, TextSpan.FromBounds(0, 0)) : Location.None;

            var messageArgs = new object[]
            {
                ex.Message,
            };

            var reportDiagnostic = Diagnostic.Create(descriptor, location, messageArgs);

            progress.Report(reportDiagnostic);
        }

        private CSharpCompilation CreateCompilation(CancellationToken cancellationToken)
        {
            var compilation = CSharpCompilation.Create("codegen")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .WithReferences(ReferencePaths.Select(p => MetadataReference.CreateFromFile(p)));
            var parseOptions = new CSharpParseOptions(preprocessorSymbols: PreprocessorSymbols);

            foreach (var sourceFile in Compile)
            {
                using (var stream = File.OpenRead(sourceFile))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var text = SourceText.From(stream);
                    compilation = compilation.AddSyntaxTrees(
                        CSharpSyntaxTree.ParseText(
                            text,
                            parseOptions,
                            sourceFile,
                            cancellationToken));
                }
            }

            return compilation;
        }

        private async Task<ImmutableDictionary<INamedTypeSymbol, MetaType>> GetAllTypeDefinitionsAsync(CSharpCompilation compilation)
        {
            var result = new ConcurrentDictionary<INamedTypeSymbol, MetaType>(SymbolEqualityComparer.Default);

            async Task TryAdd(INamedTypeSymbol typeSymbol)
            {
                var syntax = await typeSymbol.DeclaringSyntaxReferences[0].GetSyntaxAsync();
                var inputSemanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
                result.TryAdd(typeSymbol, new MetaType(typeSymbol, syntax as BaseTypeDeclarationSyntax, inputSemanticModel, this));
            }

            await Task.WhenAll(GetAllTypeSymbolsVisitor.Execute(compilation).Select(TryAdd));
            return result.ToImmutableDictionary(SymbolEqualityComparer.Default);
        }

        private async Task<bool> HasChangedAsync(IImmutableList<MetaType> types)
        {
            var processed = new HashSet<MetaType>(MetaType.DefaultComparer);

            async Task<bool> HasAnyAncestorChangedAsync(MetaType metaType)
            {
                var hasChanged = false;
                metaType = await metaType.GetDirectAncestorAsync();
                if (!metaType.IsDefault)
                {
                    hasChanged = processed.Add(metaType) && metaType.HasChanged();
                    if (!hasChanged)
                    {
                        hasChanged = await HasAnyAncestorChangedAsync(metaType);
                    }
                }
                return hasChanged;
            }

            async Task<bool> HasAnyDescendentChangedAsync(MetaType metaType)
            {
                var descendendents = await metaType.GetDirectDescendentsAsync();
                return descendendents.Any(d => processed.Add(metaType) && d.HasChanged());
            }

            async Task<bool> HasChangedAsync(MetaType metaType)
            {
                var hasChanged = processed.Add(metaType) && metaType.HasChanged();
                if (!hasChanged)
                {
                    hasChanged = await HasAnyAncestorChangedAsync(metaType);
                    if (!hasChanged)
                    {
                        if (await metaType.IsAbstractTypeAsync())
                        {
                            hasChanged = await HasAnyDescendentChangedAsync(metaType);
                        }
                    }
                }
                return hasChanged;
            }

            foreach (var metaType in types)
            {
                try
                {
                    if (await HasChangedAsync(metaType))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    ReportError(this.progress, "CGR004", metaType.DeclarationSyntax.SyntaxTree, ex);
                }
            }
            return false;
        }

        private Assembly LoadAssembly(AssemblyName assemblyName)
        {
            var matchingRefAssemblies = from refPath in ReferencePaths
                                        where Path.GetFileNameWithoutExtension(refPath).Equals(assemblyName.Name, StringComparison.OrdinalIgnoreCase)
                                        select refPath;
            var matchingAssemblies = from path in GeneratorAssemblySearchPaths
                                     from file in Directory.EnumerateFiles(path, $"{assemblyName.Name}.dll", SearchOption.TopDirectoryOnly)
                                     where AllowedAssemblyExtensions.Contains(Path.GetExtension(file))
                                     select file;

            var matchingRefAssembly = matchingRefAssemblies.Concat(matchingAssemblies).FirstOrDefault();
            if (matchingRefAssembly != null)
            {
                loadedAssemblies.Add(matchingRefAssembly);
                return LoadAssembly(matchingRefAssembly);
            }

            return Assembly.Load(assemblyName);
        }

        private Assembly LoadAssembly(string path)
        {
            if (assembliesByPath.ContainsKey(path))
                return assembliesByPath[path];

            var loadContext = AssemblyLoadContext.GetLoadContext(GetType().GetTypeInfo().Assembly);
            var assembly = loadContext.LoadFromAssemblyPath(path);

            var newDependencyContext = DependencyContext.Load(assembly);
            if (newDependencyContext != null)
                dependencyContext = dependencyContext.Merge(newDependencyContext);
            var basePath = Path.GetDirectoryName(path);
            if (!directoriesWithResolver.Contains(basePath))
            {
                assemblyResolver = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[]
                {
                    new AppBaseCompilationAssemblyResolver(basePath),
                    assemblyResolver,
                });
                directoriesWithResolver.Add(basePath);
            }

            assembliesByPath.Add(path, assembly);
            return assembly;
        }

        private Assembly ResolveAssembly(AssemblyLoadContext context, AssemblyName name)
        {
            var library = FindMatchingLibrary(dependencyContext.RuntimeLibraries, name);
            if (library == null)
                return null;
            var wrapper = new CompilationLibrary(
                library.Type,
                library.Name,
                library.Version,
                library.Hash,
                library.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
                library.Dependencies,
                library.Serviceable);

            var assemblyPaths = new List<string>();
            assemblyResolver.TryResolveAssemblyPaths(wrapper, assemblyPaths);

            if (assemblyPaths.Count == 0)
            {
                var matches = from refAssemblyPath in ReferencePaths
                              where Path.GetFileNameWithoutExtension(refAssemblyPath).Equals(name.Name, StringComparison.OrdinalIgnoreCase)
                              select context.LoadFromAssemblyPath(refAssemblyPath);
                return matches.FirstOrDefault();
            }

            return assemblyPaths.Select(context.LoadFromAssemblyPath).FirstOrDefault();
        }

        private void SaveGeneratorAssemblyList(string assemblyListPath)
        {
            // Union our current list with the one on disk, since our incremental code generation
            // may have skipped some up-to-date files, resulting in fewer assemblies being loaded
            // this time.
            var assemblyPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(assemblyListPath))
            {
                assemblyPaths.UnionWith(File.ReadAllLines(assemblyListPath));
            }

            assemblyPaths.UnionWith(loadedAssemblies);

            File.WriteAllLines(assemblyListPath, assemblyPaths);
        }

        private async Task<bool> TransformFileAsync(string outputFilePath, IImmutableList<MetaType> metaTypes, DateTime assembliesLastModified, CancellationToken cancellationToken = default)
        {
            var retriesLeft = 3;

            var lastWritten = File.Exists(outputFilePath) ? File.GetLastWriteTime(outputFilePath) : DateTime.MinValue;
            var hasChanges = assembliesLastModified > lastWritten || (await HasChangedAsync(metaTypes));
            if (hasChanges)
            {
                var (generatedSyntaxTree, anyTypesGenerated) = await DocumentTransform
                    .TransformAsync(this, metaTypes)
                    .ConfigureAwait(false);
                do
                {
                    try
                    {
                        using var outputFileStream = File.OpenWrite(outputFilePath);
                        if (anyTypesGenerated)
                        {
                            using var outputWriter = new StreamWriter(outputFileStream);
                            var outputText = await generatedSyntaxTree.GetTextAsync(cancellationToken);
                            outputText.Write(outputWriter);
                            await outputWriter.FlushAsync();
                            outputFileStream.SetLength(outputFileStream.Position);
                        }
                        else
                        {
                            outputFileStream.SetLength(0);
                        }
                        return anyTypesGenerated;
                    }
                    catch (IOException ex) when (ex.HResult == ProcessCannotAccessFileHR && retriesLeft > 0)
                    {
                        retriesLeft--;
                        await Task.Delay(200).ConfigureAwait(false);
                    }
                }
                while (true);
            }
            return File.Exists(outputFilePath) && (new FileInfo(outputFilePath)).Length > 0;
        }
    }
}
