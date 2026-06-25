using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace DataCatalyst.V2;

[Generator]
public sealed class AspectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var manifest = context.AdditionalTextsProvider
            .Where(f => Path.GetFileName(f.Path).Equals("schema.json", StringComparison.OrdinalIgnoreCase))
            .Select((t, _) => t.GetText()?.ToString() ?? "")
            .Collect()
            .Select((ar, _) => {
                var aspects = new Dictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase);
                var mapping = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var entries = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var json in ar) {
                    if (string.IsNullOrEmpty(json)) continue;
                    try {
                        using var d = System.Text.Json.JsonDocument.Parse(json); var r = d.RootElement;
                        if (r.TryGetProperty("$aspects", out var ae) && ae.ValueKind == System.Text.Json.JsonValueKind.Object)
                            foreach (var a in ae.EnumerateObject())
                                aspects[a.Name] = a.Value.EnumerateObject()
                                    .Select(f => (f.Name, JType(f.Value.GetString() ?? "string"))).ToList();
                        if (r.TryGetProperty("$mapping", out var mp) && mp.ValueKind == System.Text.Json.JsonValueKind.Object)
                            foreach (var x in mp.EnumerateObject())
                                if (!mapping.ContainsKey(x.Name))
                                    mapping[x.Name] = x.Value.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s != "").ToList();
                        if (r.TryGetProperty("$entries", out var en) && en.ValueKind == System.Text.Json.JsonValueKind.Object)
                            foreach (var x in en.EnumerateObject())
                                if (!entries.ContainsKey(x.Name))
                                    entries[x.Name] = x.Value.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s != "").ToList();
                    } catch { }
                }
                return (aspects, mapping, entries);
            });

        context.RegisterSourceOutput(manifest, (spc, data) => {
            var (aspects, mapping, entries) = data;
            if (aspects.Count == 0 && mapping.Count == 0 && entries.Count == 0) return;
            var mem = new List<MemberDeclarationSyntax>();
            var ini = new List<StatementSyntax>();
            var helperMethods = new List<MemberDeclarationSyntax>();

            int deserIdx = 0;
            foreach (var kv in aspects) {
                var nm = Sanitize(kv.Key);
                var props = string.Join("\n", kv.Value.Select(f => $"    public {f.Item2} {Sanitize(f.Item1)} {{ get; set; }}"));
                var s = ParseMemberDeclaration($"public record struct {nm}\n{{\n{props}\n}}");
                if (s != null) {
                    s = s.WithAttributeLists(SingletonList(AttributeList(SingletonSeparatedList(
                        Attribute(ParseName("global::DataCatalyst.Attributes.GameAspect"))))));
                    mem.Add(s);
                }
                ini.Add(ParseStatement($"global::DataCatalyst.Storage.AspectTypeRegistry.Register(typeof({nm}));\n")!);

                // Deserializer helper method — each field = TryGetValue then cast or default
                var setters = kv.Value.Select(f =>
                    $"    {Sanitize(f.Item1)} = __dict.TryGetValue(\"{f.Item1}\", out var __v{deserIdx}_{Sanitize(f.Item1)}) && __v{deserIdx}_{Sanitize(f.Item1)} != null ? {Cast(f.Item2, "__v" + deserIdx + "_" + Sanitize(f.Item1))} : default({f.Item2})");
                var helperName = $"__Deser_{nm}";
                helperMethods.Add(ParseMemberDeclaration(
                    $"static global::DataCatalyst.Generated.{nm} {helperName}(object? __n) {{\n" +
                    $"    if (!(__n is global::System.Collections.Generic.Dictionary<string, object?> __dict))\n" +
                    $"        return new global::DataCatalyst.Generated.{nm}();\n" +
                    $"    return new global::DataCatalyst.Generated.{nm} {{\n" +
                    string.Join(",\n", setters) + "\n" +
                    $"    }};\n" +
                    "}")!);
                ini.Add(ParseStatement(
                    $"global::DataCatalyst.Storage.AspectTypeRegistry.RegisterDeserializer(typeof({nm}), (object __n) => {helperName}(__n));\n")!);
                deserIdx++;
            }

            // Concept structs
            var allConcepts = new HashSet<string>(mapping.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries.Values) foreach (var c in e) allConcepts.Add(c);
            foreach (var c in allConcepts) {
                var cn = Sanitize(c);
                var s = ParseMemberDeclaration($"public struct {cn} : global::DataCatalyst.IConcept {{ }}");
                if (s != null) mem.Add(s);
            }

            // Entry structs
            foreach (var kv in entries) {
                var en = Sanitize(kv.Key);
                var ifaces = string.Join(", ", kv.Value.Select(c =>
                    $"global::DataCatalyst.IBelongTo<global::DataCatalyst.Generated.{Sanitize(c)}>"));
                var s = ParseMemberDeclaration($"public record struct {en} : global::DataCatalyst.IEntry, {ifaces} {{ }}");
                if (s != null) mem.Add(s);
                var typeArgs = string.Join(", ", kv.Value.Select(c => $"typeof(global::DataCatalyst.Generated.{Sanitize(c)})"));
                ini.Add(ParseStatement(
                    $"global::DataCatalyst.Registry.EntryRegistry.Register<global::DataCatalyst.Generated.{en}>({typeArgs});\n")!);
            }

            // Typed pools
            foreach (var kv in mapping) {
                var cn = Sanitize(kv.Key);
                var fas = kv.Value.Where(a => aspects.ContainsKey(a))
                    .Select(a => (Sanitize(a), $"global::DataCatalyst.Generated.{Sanitize(a)}")).ToList();
                if (fas.Count == 0) continue;
                var sn = $"{cn}Aspects";
                var fields = string.Join("\n", fas.Select(f => $"    public {f.Item2} {f.Item1};"));
                var takeCases = string.Join("\n        ", fas.Select(f =>
                    $"if (typeof(T) == typeof({f.Item2})) return ref global::System.Runtime.CompilerServices.Unsafe.As<{f.Item2}, T>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in this.{f.Item1}));"));
                mem.Add(ParseMemberDeclaration($"public struct {sn} {{ {fields} public ref readonly T Take<T>() where T : struct {{ {takeCases} throw new global::System.ArgumentException($\"Aspect '{{typeof(T).Name}}' not found in {sn}\"); }} }}"));
                var setCases = string.Join("\n        ", fas.Select(f =>
                    $"if (typeof(T) == typeof({f.Item2})) {{ _data[index].{f.Item1} = ({f.Item2})(object)value; return; }}"));
                var setRawCases = string.Join("\n        ", fas.Select(f =>
                    $"if (type == typeof({f.Item2})) {{ _data[index].{f.Item1} = ({f.Item2})value; return; }}"));
                mem.Add(ParseMemberDeclaration($@"
public sealed class {cn}Pool : global::DataCatalyst.Storage.IStoragePool {{
    private {sn}[] _data = global::System.Array.Empty<{sn}>();
    public int Count => _data.Length;
    public void Resize(int size) => global::System.Array.Resize(ref _data, size);
    public ref readonly T Get<T>(int index) where T : struct {{ if (index < 0 || index >= _data.Length) throw new global::System.IndexOutOfRangeException(); return ref _data[index].Take<T>(); }}
    public void Set<T>(int index, T value) where T : struct {{ if (index < 0 || index >= _data.Length) throw new global::System.IndexOutOfRangeException(); {setCases} }}
    public void SetRaw(int index, global::System.Type type, object value) {{ if (index < 0 || index >= _data.Length) return; {setRawCases} }}
}}"));
                ini.Add(ParseStatement(
                    $"global::DataCatalyst.Registry.EntryRegistry.RegisterPool(typeof({cn}), () => new {cn}Pool());\n")!);
            }

            // SchemaGen class = ModuleInitializer + inline deserializer helpers
            if (helperMethods.Count > 0)
                helperMethods.Insert(0, MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Init")
                    .WithAttributeLists(SingletonList(AttributeList(SingletonSeparatedList(
                        Attribute(ParseName("System.Runtime.CompilerServices.ModuleInitializer"))))))
                    .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword)))
                    .WithBody(Block(ini)));

            if (helperMethods.Count > 0)
                mem.Add(ClassDeclaration("SchemaGen")
                    .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword)))
                    .WithMembers(List(helperMethods)));

            if (mem.Count == 0) return;
            spc.AddSource("SchemaAspects.g.cs",
                SourceText.From(CompilationUnit()
                    .WithLeadingTrivia(Comment("// <auto-generated/>"))
                    .WithMembers(SingletonList<MemberDeclarationSyntax>(
                        NamespaceDeclaration(ParseName("DataCatalyst.Generated"))
                            .WithMembers(List(mem))))
                    .NormalizeWhitespace().ToFullString(), Encoding.UTF8));
        });
    }

    static string Cast(string type, string varName) => type switch {
        "int" => $"global::System.Convert.ToInt32({varName})",
        "long" => $"global::System.Convert.ToInt64({varName})",
        "float" => $"global::System.Convert.ToSingle({varName})",
        "double" => $"global::System.Convert.ToDouble({varName})",
        "bool" => $"global::System.Convert.ToBoolean({varName})",
        _ => $"({type}){varName}",
    };

    static string Sanitize(string n) {
        if (string.IsNullOrEmpty(n)) return "Unknown";
        var c = n.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        return new string(c).Length > 0 ? new string(c) : "_";
    }

    static string JType(string t) => t.ToLowerInvariant() switch {
        "int" or "int32" => "int", "long" or "int64" => "long",
        "float" or "single" => "float", "double" => "double",
        "bool" or "boolean" => "bool", "string" => "string",
        _ => "string",
    };
}
