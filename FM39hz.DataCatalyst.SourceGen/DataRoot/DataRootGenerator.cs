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
                    public Type[]? Plugins { get; init; }
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

            var defaultPlugin = new DefaultSchemaPlugin();
            var plugins = DataRootPluginRegistry.GetPlugins();

            foreach (var rootPrefix in roots) {
                var ns = PathToNamespace(rootPrefix);
                var allSource = new StringBuilder();

                foreach (var (path, content) in files) {
                    if (!path.StartsWith(rootPrefix, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    var relativePath = path.Substring(rootPrefix.Length);

                    // Core: always generates struct from kind/fields/inherits
                    var pctx = new PluginContext(path, relativePath, content, ns, rootPrefix, spc);
                    var baseResult = defaultPlugin.Process(pctx);

                    if (baseResult.SourceCode.Length > 0)
                        allSource.AppendLine(baseResult.SourceCode);
                    foreach (var diag in baseResult.Diagnostics)
                        spc.ReportDiagnostic(diag);
                    foreach (var kvp in baseResult.AdditionalSources)
                        spc.AddSource(kvp.Key, SourceText.From(kvp.Value, Encoding.UTF8));

                    // Plugins: additive processing on top of core struct
                    foreach (var plugin in plugins) {
                        if (plugin == defaultPlugin) continue;
                        if (!plugin.CanHandle(relativePath, content)) continue;

                        var extra = plugin.Process(pctx);
                        if (extra.SourceCode.Length > 0)
                            allSource.AppendLine(extra.SourceCode);
                        foreach (var diag in extra.Diagnostics)
                            spc.ReportDiagnostic(diag);
                        foreach (var kvp in extra.AdditionalSources)
                            spc.AddSource(kvp.Key, SourceText.From(kvp.Value, Encoding.UTF8));
                    }
                }

                var sanitized = rootPrefix.Replace('/', '_').Replace('\\', '_').Replace(":", "");
                if (allSource.Length > 0)
                    spc.AddSource($"DataRoot_{sanitized}.g.cs", SourceText.From(allSource.ToString(), Encoding.UTF8));
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
