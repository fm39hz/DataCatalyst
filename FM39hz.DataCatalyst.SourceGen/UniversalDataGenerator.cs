namespace FM39hz.DataCatalyst;

using System.Collections.Generic;
using System.Collections.Immutable;
using FM39hz.DataCatalyst.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed class UniversalDataGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        context.RegisterPostInitializationOutput(static ctx => {
            ctx.AddSource("CatalystDataAttribute.g.cs", """
                namespace FM39hz.DataCatalyst;

                [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
                public sealed class CatalystDataAttribute : System.Attribute {
                    public string JsonPath { get; }
                    public string EntryPoint { get; }
                    public System.Type TemplateType { get; }
                    public string KeyField { get; init; } = string.Empty;
                    public int Backend { get; init; } = 0;
                    public System.Type[]? RefTo { get; init; }
                    public int LoadMode { get; init; } = 0;
                    public string SchemaVersion { get; init; } = "";

                    public CatalystDataAttribute(string jsonPath, string entryPoint = "", System.Type templateType = null) {
                        JsonPath = jsonPath;
                        EntryPoint = entryPoint;
                        TemplateType = templateType;
                    }
                }

                [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
                public sealed class ModPluginAttribute : System.Attribute {
                    public string Name { get; }
                    public string[] Dependencies { get; }

                    public ModPluginAttribute(string name, string[] dependencies = null) {
                        Name = name;
                        Dependencies = dependencies ?? [];
                    }
                }

                public static class DepParser {
                    public static (string Name, int Major, int Minor, int Patch) Parse(string dep) {
                        var at = dep.LastIndexOf('@');
                        if (at < 0) return (dep, 0, 0, 0);
                        var name = dep.Substring(0, at);
                        var ver = dep.Substring(at + 1);
                        var parts = ver.Split('.');
                        var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
                        var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                        var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
                        return (name, major, minor, patch);
                    }

                    public static bool Satisfies(string required, string available) {
                        var (_, rMajor, rMinor, rPatch) = Parse(required);
                        var (_, aMajor, aMinor, aPatch) = Parse(available);
                        if (rMajor == 0) return true;
                        if (aMajor != rMajor) return false;
                        if (aMinor > rMinor) return true;
                        if (aMinor < rMinor) return false;
                        return aPatch >= rPatch;
                    }
                }
                """);
        });

        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DcConstants.CATALYST_DATA_ATTRIBUTE_METADATA,
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => TargetInfo.Extract(ctx))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        var modPlugins = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DcConstants.MOD_PLUGIN_ATTRIBUTE_METADATA,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => ExtractModPlugin(ctx))
            .Where(static p => p.HasValue)
            .Select(static (p, _) => p!.Value)
            .Collect();

        var combined = context.AdditionalTextsProvider.Collect()
            .Combine(targets.Collect())
            .Combine(context.CompilationProvider)
            .Combine(modPlugins);

        context.RegisterSourceOutput(
            combined,
            static (spc, payload) => {
                var (((additionalTexts, ts), compilation), modPluginInfos) = payload;
                if (ts.IsDefaultOrEmpty) return;

                var hasModdingPlugin = false;
                foreach (var attr in compilation.ReferencedAssemblyNames) {
                    if (attr.Name == "FM39hz.DataCatalyst.Plugins.Modding.Runtime") {
                        hasModdingPlugin = true;
                        break;
                    }
                }

                PipelineDriver.Reset();
                var sorted = TopoSortCatalogs(ts);
                foreach (var t in sorted) {
                    PipelineDriver.Run(spc, additionalTexts, t, hasModdingPlugin);
                }

                if (!modPluginInfos.IsDefaultOrEmpty) {
                    EmitModPluginRegistrations(spc, modPluginInfos);
                }
            });
    }

    private static void EmitModPluginRegistrations(SourceProductionContext spc, ImmutableArray<(string Name, string FullType, string[] Dependencies)> plugins) {
        var sb = new System.Text.StringBuilder();
        sb.Append("// <auto-generated/>\n#nullable enable\n\nnamespace FM39hz.DataCatalyst.Runtime;\n\npublic static partial class ModPluginRegistrations {\n");
        var seen = new HashSet<string>();
        foreach (var (name, fullType, _) in plugins) {
            if (!seen.Add(name)) continue;
            sb.Append("\t[System.Runtime.CompilerServices.ModuleInitializer]\n");
            sb.Append("\tinternal static void Register_").Append(System.Text.RegularExpressions.Regex.Replace(name, "[^a-zA-Z0-9]", "_")).Append("() =>\n");
            sb.Append("\t\tglobal::FM39hz.DataCatalyst.Runtime.PluginRegistry.Register(new ").Append(fullType).AppendLine("());\n");
        }
        sb.Append("}");
        spc.AddSource("ModPluginRegistrations.g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(sb.ToString(), System.Text.Encoding.UTF8));
    }

    private static (string Name, string FullType, string[] Dependencies)? ExtractModPlugin(GeneratorAttributeSyntaxContext ctx) {
        if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
        AttributeData? attr = null;
        foreach (var a in ctx.Attributes) { attr = a; break; }
        if (attr is null) return null;

        var name = string.Empty;
        var dependencies = System.Array.Empty<string>();

        if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string n) name = n;
        if (attr.ConstructorArguments.Length >= 2 && attr.ConstructorArguments[1].Values is var deps) {
            var list = new List<string>();
            foreach (var d in deps) {
                var s = d.Value?.ToString();
                if (!string.IsNullOrEmpty(s)) list.Add(s!);
            }
            dependencies = [.. list];
        }

        foreach (var na in attr.NamedArguments) {
            switch (na.Key) {
                case "Name" when na.Value.Value is string ns: name = ns; break;
                case "Dependencies" when na.Value.Values is { Length: > 0 } vs:
                    var list = new List<string>();
                    foreach (var v in vs) {
                        var s = v.Value?.ToString();
                        if (!string.IsNullOrEmpty(s)) list.Add(s!);
                    }
                    dependencies = [.. list];
                    break;
            }
        }

        if (string.IsNullOrEmpty(name)) name = type.Name;
        var fullType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return (Name: name, FullType: fullType, Dependencies: dependencies);
    }

    private static ImmutableArray<TargetInfo> TopoSortCatalogs(ImmutableArray<TargetInfo> targets) {
        var map = new Dictionary<string, TargetInfo>();
        var indegree = new Dictionary<string, int>();
        var edges = new Dictionary<string, List<string>>();

        foreach (var t in targets) {
            map[t.SimpleName] = t;
            indegree[t.SimpleName] = 0;
            edges[t.SimpleName] = [];
        }

        foreach (var t in targets) {
            foreach (var rt in t.RefToTargets) {
                var dot = rt.LastIndexOf('.');
                var simple = dot >= 0 ? rt.Substring(dot + 1) : rt;
                if (!map.ContainsKey(simple)) continue;
                edges[simple].Add(t.SimpleName);
                indegree[t.SimpleName]++;
            }
        }

        var ready = new List<TargetInfo>();
        foreach (var t in targets) {
            if (indegree[t.SimpleName] == 0) ready.Add(t);
        }
        ready.Sort(static (a, b) => string.CompareOrdinal(a.SimpleName, b.SimpleName));

        var ordered = new List<TargetInfo>(targets.Length);
        while (ready.Count > 0) {
            var cur = ready[0];
            ready.RemoveAt(0);
            ordered.Add(cur);
            foreach (var child in edges[cur.SimpleName]) {
                indegree[child]--;
                if (indegree[child] == 0) ready.Add(map[child]);
            }
            ready.Sort(static (a, b) => string.CompareOrdinal(a.SimpleName, b.SimpleName));
        }

        if (ordered.Count < targets.Length) {
            ordered.Clear();
            foreach (var t in targets) ordered.Add(t);
        }

        return [.. ordered];
    }
}
