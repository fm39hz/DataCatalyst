namespace DataCatalyst.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DataCatalyst.SourceGen.Generation;
using DataCatalyst.SourceGen.Models;
using DataCatalyst.SourceGen.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed class AspectGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var aspectDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "DataCatalyst.Attributes.GameAspectAttribute",
                predicate: (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax,
                transform: Helpers.GetAspectInfo)
            .Where(x => x is not null);

        var conceptDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "DataCatalyst.Attributes.GameConceptAttribute",
                predicate: (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax,
                transform: Helpers.GetConceptInfo)
            .Where(x => x is not null);

        var jsonData = context.AdditionalTextsProvider
            .Where(f => Path.GetExtension(f.Path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            .Select((t, _) => {
                try {
                    var content = t.GetText()?.ToString() ?? "";
                    if (string.IsNullOrEmpty(content)) return new JsonDataInfo(new BeingEntry[0]);
                    using var d = JsonDocument.Parse(content);
                    return SourceEmitter.ParseBeingFile(d.RootElement);
                }
                catch (JsonException) { return new JsonDataInfo(new BeingEntry[0]); }
            })
            .Where(x => x.Beings.Length > 0)
            .Collect()
            .Select((ar, _) => {
                var merged = new Dictionary<string, BeingEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var fileData in ar) {
                    foreach (var being in fileData.Beings) {
                        if (!merged.ContainsKey(being.Key))
                            merged[being.Key] = being;
                    }
                }
                return new JsonDataInfo(merged.Values.ToArray());
            });

        var ontologyData = context.AdditionalTextsProvider
            .Collect()
            .Select((ar, _) => {
                var sb = new StringBuilder();
                foreach (var text in ar) {
                    var content = text.GetText()?.ToString() ?? "";
                    if (string.IsNullOrEmpty(content)) continue;
                    var ext = Path.GetExtension(text.Path);
                    if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase)) {
                        try {
                            using var d = JsonDocument.Parse(content);
                            var r = d.RootElement;
                            if (r.ValueKind == JsonValueKind.Object &&
                                ((r.TryGetProperty("concepts", out var ck) && ck.ValueKind == JsonValueKind.Object) ||
                                 (r.TryGetProperty("aspects", out var ak) && ak.ValueKind == JsonValueKind.Object) ||
                                 (r.TryGetProperty("relations", out var rk) && rk.ValueKind == JsonValueKind.Object))) {
                                if (sb.Length > 0) sb.Append('\0');
                                sb.Append(content);
                            }
                        }
                        catch (JsonException) { }
                    }
                    else if (ext.Equals(".xml", StringComparison.OrdinalIgnoreCase)) {
                        if (content.Contains("<concepts>") || content.Contains("<ontology>")) {
                            if (sb.Length > 0) sb.Append('\0');
                            sb.Append(content);
                        }
                    }
                    else if (ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
                          || ext.Equals(".toml", StringComparison.OrdinalIgnoreCase)
                          || ext.Equals(".yml", StringComparison.OrdinalIgnoreCase)) {
                        if (sb.Length > 0) sb.Append('\0');
                        sb.Append(content);
                    }
                }
                return new OntologyInfo(sb.ToString());
            });

        var frameworkData = context.CompilationProvider
            .Select((compilation, _) => {
                var conceptAttr = compilation.GetTypeByMetadataName("DataCatalyst.Attributes.GameConceptAttribute");
                var conceptInterface = compilation.GetTypeByMetadataName("DataCatalyst.IConcept");
                var aspectAttr = compilation.GetTypeByMetadataName("DataCatalyst.Attributes.GameAspectAttribute");
                var nsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var cNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var aNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var aspects = new List<AspectInfo?>();
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols) {
                    var aname = assembly.Name;
                    if (aname.StartsWith("System") || aname.StartsWith("Microsoft")
                        || aname == "mscorlib" || aname == "netstandard"
                        || aname.StartsWith("Windows") || aname.StartsWith("Mono"))
                        continue;
                    SymbolScanner.FindConceptsInNamespace(assembly.GlobalNamespace, conceptAttr, conceptInterface, cNames, nsMap);
                    SymbolScanner.FindAspectsInNamespaceEx(assembly.GlobalNamespace, aspectAttr, aNames, aspects);
                }
                return new FrameworkData(nsMap, cNames, aNames, aspects);
            });

        var combined = aspectDeclarations.Collect()
            .Combine(conceptDeclarations.Collect())
            .Combine(jsonData)
            .Combine(ontologyData)
            .Combine(frameworkData);

        context.RegisterSourceOutput(combined, (spc, input) => {
            var aspects = input.Left.Left.Left.Left;
            var concepts = input.Left.Left.Left.Right;
            var jsonData = input.Left.Left.Right;
            var ontologyData = input.Left.Right;
            var framework = input.Right;

            // ── Build maps ──
            var aspectDict = aspects.ToDictionary(a => a!.Name, a => a!, StringComparer.OrdinalIgnoreCase);
            var localAspectNames = new HashSet<string>(aspects.Select(a => a!.Name), StringComparer.OrdinalIgnoreCase);
            var aspectNames = new HashSet<string>(localAspectNames, StringComparer.OrdinalIgnoreCase);
            var conceptNsMap = new Dictionary<string, string>(framework.ConceptNsMap, StringComparer.OrdinalIgnoreCase);
            var conceptNames = new HashSet<string>(framework.ConceptNames, StringComparer.OrdinalIgnoreCase);
            foreach (var c in concepts) {
                if (c != null) {
                    conceptNames.Add(c.Name);
                    if (!conceptNsMap.ContainsKey(c.Name)) conceptNsMap[c.Name] = c.Namespace;
                }
            }
            foreach (var an in framework.AspectNames) aspectNames.Add(an);

            // ── Parse ontology ──
            var ontologyRequires = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var ontologySuggests = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var ontologyAspects = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var hasOntology = !string.IsNullOrEmpty(ontologyData?.ConceptsJson);
            if (hasOntology) {
                var parts = ontologyData!.ConceptsJson.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                var parsers = new List<IOntologyParser> { new BuiltinJsonParser() };
                foreach (var part in parts) {
                    var ext = part.TrimStart().StartsWith("<") ? ".xml" : ".json";
                    var ontFile = new OntologyFile("ontology" + ext, part);
                    foreach (var parser in parsers) {
                        if (parser.CanHandle(ontFile)) {
                            var builder = new OntologyBuilder();
                            parser.Parse(ontFile, builder);
                            foreach (var kv in builder.Requires) ontologyRequires[kv.Key] = kv.Value;
                            foreach (var kv in builder.Suggests) ontologySuggests[kv.Key] = kv.Value;
                            foreach (var kv in builder.AspectFields) ontologyAspects[kv.Key] = kv.Value;
                            break;
                        }
                    }
                }
            }

            var allOntologyConcepts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (hasOntology) {
                foreach (var k in ontologyRequires.Keys) allOntologyConcepts.Add(k);
                foreach (var k in ontologySuggests.Keys) allOntologyConcepts.Add(k);
            }

            var knownConceptNames = new HashSet<string>(conceptNames, StringComparer.OrdinalIgnoreCase);
            if (hasOntology) {
                foreach (var cn in allOntologyConcepts) knownConceptNames.Add(cn);
            }

            // ── Build beingConcepts + conceptAspects from JSON ──
            var frameworkAspects = new List<AspectInfo?>(framework.Aspects);
            var beingConcepts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var conceptAspects = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (hasOntology) {
                foreach (var kv in ontologyRequires)
                    conceptAspects[kv.Key] = new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var being in jsonData.Beings) {
                var cList = new List<string>();
                var allAspectsList = new List<string>();
                foreach (var f in being.Fields) {
                    if (f.Name.StartsWith("$")) {
                        var cn = f.Name.Substring(1);
                        if (knownConceptNames.Contains(cn)) {
                            if (!cList.Contains(cn)) cList.Add(cn);
                            if (!conceptAspects.TryGetValue(cn, out var aSet))
                                conceptAspects[cn] = aSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var n in f.NestedFields) aSet.Add(n);
                        }
                        else {
                            spc.ReportDiagnostic(Diagnostic.Create(
                                new DiagnosticDescriptor("DC0001", "Unknown Concept",
                                    $"Unknown concept '{cn}' specified with '$' prefix in being '{being.Key}'",
                                    "DataCatalyst", DiagnosticSeverity.Warning, isEnabledByDefault: true),
                                Location.None));
                        }
                    }
                    else {
                        allAspectsList.Add(f.Name);
                    }
                }
                beingConcepts[being.Key] = cList;
                foreach (var conceptName in cList) {
                    if (!conceptAspects.TryGetValue(conceptName, out var aSet))
                        conceptAspects[conceptName] = aSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var aspect in allAspectsList) aSet.Add(aspect);
                }
            }

            // Combine aspects
            var combinedAspects = new List<AspectInfo?>();
            combinedAspects.AddRange(aspects);
            foreach (var fa in frameworkAspects) {
                if (fa != null && !combinedAspects.Any(a => a?.Name == fa.Name)) {
                    combinedAspects.Add(fa);
                    if (!aspectDict.ContainsKey(fa.Name)) aspectDict[fa.Name] = fa;
                }
            }
            if (hasOntology) {
                foreach (var kv in ontologyAspects) {
                    if (aspectDict.TryGetValue(kv.Key, out var ai) && !combinedAspects.Any(a => a?.Name == kv.Key))
                        combinedAspects.Add(ai);
                }
            }

            // ── Generate outputs via SourceEmitter ──
            var emitter = new SourceEmitter(aspectDict, aspectNames, localAspectNames,
                conceptNsMap, conceptNames, beingConcepts, conceptAspects, combinedAspects);

            var conceptsWriter = new CodeWriter();
            emitter.EmitConceptStructs(allOntologyConcepts, hasOntology, conceptsWriter, spc);
            if (conceptsWriter.HasContent)
                spc.AddSource("Concepts.g.cs", conceptsWriter.ToSourceText());

            var aspectsWriter = new CodeWriter();
            emitter.EmitOntologyAspects(ontologyAspects, hasOntology, aspectsWriter, spc);
            if (aspectsWriter.HasContent)
                spc.AddSource("Aspects.g.cs", aspectsWriter.ToSourceText());

            var deserWriter = new CodeWriter();
            emitter.EmitDeserializerHelpers(ontologyRequires, ontologySuggests, allOntologyConcepts, hasOntology, deserWriter, spc);
            if (deserWriter.HasContent)
                spc.AddSource("Deserializers.g.cs", deserWriter.ToSourceText());

            var beingsWriter = new CodeWriter();
            emitter.EmitBeingStructs(beingsWriter, spc);
            if (beingsWriter.HasContent)
                spc.AddSource("Beings.g.cs", beingsWriter.ToSourceText());

            var poolsWriter = new CodeWriter();
            emitter.EmitPools(poolsWriter, spc);
            if (poolsWriter.HasContent)
                spc.AddSource("Pools.g.cs", poolsWriter.ToSourceText());
        });
    }
}
