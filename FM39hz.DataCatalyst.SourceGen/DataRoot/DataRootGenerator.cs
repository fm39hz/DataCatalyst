using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using FM39hz.DataCatalyst.DataRoot;

namespace FM39hz.DataCatalyst;

[Generator]
public sealed class DataRootGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // Emit [DataRoot] attribute
        context.RegisterPostInitializationOutput(static ctx => {
            ctx.AddSource("DataRootAttribute.g.cs", SourceText.From("""
                using System;
                [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
                public sealed class DataRootAttribute : Attribute {
                    public string Directory { get; }
                    public DataRootAttribute(string directory) => Directory = directory;
                }
                """, Encoding.UTF8));
        });

        // Collect DataRoot attributes + additional files
        var dataRoots = context.CompilationProvider
            .Select(static (comp, _) => ExtractDataRoots(comp));

        var additionalFiles = context.AdditionalTextsProvider
            .Where(static t => t.Path.EndsWith(".json"))
            .Select(static (t, _) => (Path: t.Path, Content: t.GetText()?.ToString() ?? ""))
            .Where(static t => t.Content.Length > 0)
            .Collect();

        var combined = dataRoots.Combine(additionalFiles);

        context.RegisterSourceOutput(combined, static (spc, payload) => {
            var (roots, files) = payload;
            if (roots.IsDefaultOrEmpty) return;

            foreach (var rootPrefix in roots) {
                // Scan → inheritance graph → emit code
                var scanner = new DataRootScanner();
                scanner.Scan(rootPrefix, files);

                var graph = new InheritanceGraph();
                foreach (var s in scanner.Schemas) graph.AddSchema(s);
                foreach (var d in scanner.DataFiles) graph.AddNode(d);

                var ns = PathToNamespace(rootPrefix);
                var emitter = new NativePocoEmitter(graph, ns);
                var code = emitter.EmitAll();

                var sanitized = rootPrefix.Replace('/', '_').Replace('\\', '_').Replace(":", "");
                if (code.Length > 0)
                    spc.AddSource($"DataRoot_{sanitized}.g.cs", SourceText.From(code, Encoding.UTF8));
            }
        });
    }

    private static string PathToNamespace(string rootDir) {
        return rootDir.TrimEnd('/').Replace('/', '.').Replace('\\', '.');
    }

    private static ImmutableArray<string> ExtractDataRoots(Compilation compilation) {
        var roots = ImmutableArray.CreateBuilder<string>();
        foreach (var attr in compilation.Assembly.GetAttributes()) {
            if (attr.AttributeClass?.Name == "DataRootAttribute" &&
                attr.ConstructorArguments.Length == 1 &&
                attr.ConstructorArguments[0].Value is string dir) {
                var normalized = dir.Replace('\\', '/');
                if (!normalized.EndsWith("/")) normalized += "/";
                roots.Add(normalized);
            }
        }
        return roots.ToImmutable();
    }
}
