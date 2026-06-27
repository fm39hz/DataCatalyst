namespace DataCatalyst.V2;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

public readonly struct OntologyFile {
	public string FileName { get; }
	public string Content { get; }
	public OntologyFile(string fileName, string content) { FileName = fileName; Content = content; }
}

public sealed class OntologyBuilder {
	public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> Requires { get; } = new(System.StringComparer.OrdinalIgnoreCase);
	public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> Suggests { get; } = new(System.StringComparer.OrdinalIgnoreCase);
	public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> AspectFields { get; } = new(System.StringComparer.OrdinalIgnoreCase);
	public void AddRequires(string concept, params string[] aspects) { if (!Requires.ContainsKey(concept)) Requires[concept] = [.. aspects]; }
	public void AddSuggests(string concept, params string[] aspects) { if (!Suggests.ContainsKey(concept)) Suggests[concept] = [.. aspects]; }
	public void AddAspectFields(string aspect, System.Collections.Generic.Dictionary<string, string> fields) { if (!AspectFields.ContainsKey(aspect)) AspectFields[aspect] = fields; }
}

public interface IOntologyParser {
	bool CanHandle(in OntologyFile file);
	void Parse(in OntologyFile file, OntologyBuilder builder);
}

internal sealed class BuiltinJsonParser : IOntologyParser {
	public bool CanHandle(in OntologyFile file) {
		var ext = System.IO.Path.GetExtension(file.FileName);
		if (!ext.Equals(".json", StringComparison.OrdinalIgnoreCase)) return false;
		try {
			using var doc = JsonDocument.Parse(file.Content);
			var root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Object) return false;
			if (root.TryGetProperty("concepts", out var c) && c.ValueKind == JsonValueKind.Object) return true;
			if (root.TryGetProperty("aspects", out var a) && a.ValueKind == JsonValueKind.Object) return true;
			if (root.TryGetProperty("relations", out var r) && r.ValueKind == JsonValueKind.Object) return true;
			return false;
		}
		catch { return false; }
	}

	public void Parse(in OntologyFile file, OntologyBuilder builder) {
		try {
			using var doc = JsonDocument.Parse(file.Content);
			var root = doc.RootElement;
			if (root.TryGetProperty("aspects", out var aspRoot) && aspRoot.ValueKind == JsonValueKind.Object) {
				foreach (var aspEntry in aspRoot.EnumerateObject()) {
					var aDef = aspEntry.Value;
					if (aDef.ValueKind != JsonValueKind.Object) continue;
					if (!aDef.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object) continue;
					var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					foreach (var field in fields.EnumerateObject())
						if (field.Value.ValueKind == JsonValueKind.String)
							fieldMap[field.Name] = field.Value.GetString()!;
					if (fieldMap.Count > 0 && !builder.AspectFields.ContainsKey(aspEntry.Name))
						builder.AddAspectFields(aspEntry.Name, fieldMap);
				}
			}
			if (!root.TryGetProperty("concepts", out var concepts) || concepts.ValueKind != JsonValueKind.Object) return;
			foreach (var conceptEntry in concepts.EnumerateObject()) {
				var cName = conceptEntry.Name;
				var entry = conceptEntry.Value;
				if (entry.ValueKind != JsonValueKind.Object) continue;
				if (entry.TryGetProperty("$requires", out var req) && req.ValueKind == JsonValueKind.Array) {
					var list = new List<string>();
					foreach (var item in req.EnumerateArray())
						if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString()!);
					if (list.Count > 0 && !builder.Requires.ContainsKey(cName))
						builder.AddRequires(cName, [.. list]);
				}
				if (entry.TryGetProperty("$suggests", out var sug) && sug.ValueKind == JsonValueKind.Array) {
					var list = new List<string>();
					foreach (var item in sug.EnumerateArray())
						if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString()!);
					if (list.Count > 0 && !builder.Suggests.ContainsKey(cName))
						builder.AddSuggests(cName, [.. list]);
				}
				if (!builder.Requires.ContainsKey(cName) && !builder.Suggests.ContainsKey(cName))
					builder.AddRequires(cName);
			}
		}
		catch { }
	}
}

public sealed class AspectPropertyInfo(string name, string type) : IEquatable<AspectPropertyInfo> {
	public string Name { get; } = name;
	public string Type { get; } = type;

	public bool Equals(AspectPropertyInfo? other) {
		if (other is null) {
			return false;
		}

		return Name == other.Name && Type == other.Type;
	}

	public override bool Equals(object? obj) => Equals(obj as AspectPropertyInfo);

	public override int GetHashCode() {
		var hash = 17;
		hash = (hash * 23) + (Name?.GetHashCode() ?? 0);
		hash = (hash * 23) + (Type?.GetHashCode() ?? 0);
		return hash;
	}
}

public sealed class AspectInfo(string name, string ns, string propertiesString) : IEquatable<AspectInfo> {
	public string Name { get; } = name;
	public string Namespace { get; } = ns;
	public string PropertiesString { get; } = propertiesString;

	public bool Equals(AspectInfo? other) {
		if (other is null) {
			return false;
		}

		return Name == other.Name && Namespace == other.Namespace && PropertiesString == other.PropertiesString;
	}

	public override bool Equals(object? obj) => Equals(obj as AspectInfo);

	public override int GetHashCode() {
		var hash = 17;
		hash = (hash * 23) + (Name?.GetHashCode() ?? 0);
		hash = (hash * 23) + (Namespace?.GetHashCode() ?? 0);
		hash = (hash * 23) + (PropertiesString?.GetHashCode() ?? 0);
		return hash;
	}
}

public sealed class ConceptInfo(string name, string ns) : IEquatable<ConceptInfo> {
	public string Name { get; } = name;
	public string Namespace { get; } = ns;

	public bool Equals(ConceptInfo? other) {
		if (other is null) {
			return false;
		}

		return Name == other.Name && Namespace == other.Namespace;
	}

	public override bool Equals(object? obj) => Equals(obj as ConceptInfo);

	public override int GetHashCode() {
		var hash = 17;
		hash = (hash * 23) + (Name?.GetHashCode() ?? 0);
		hash = (hash * 23) + (Namespace?.GetHashCode() ?? 0);
		return hash;
	}
}

public sealed class JsonDataInfo(string beingsString) : IEquatable<JsonDataInfo> {
	public string BeingsString { get; } = beingsString;

	public bool Equals(JsonDataInfo? other) {
		if (other is null) {
			return false;
		}

		return BeingsString == other.BeingsString;
	}

	public override bool Equals(object? obj) => Equals(obj as JsonDataInfo);

	public override int GetHashCode() => BeingsString?.GetHashCode() ?? 0;
}

public sealed class OntologyInfo : IEquatable<OntologyInfo> {
	public string ConceptsJson { get; }

	public OntologyInfo(string conceptsJson) {
		ConceptsJson = conceptsJson;
	}

	public bool Equals(OntologyInfo? other) {
		if (other is null) return false;
		return ConceptsJson == other.ConceptsJson;
	}

	public override bool Equals(object? obj) => Equals(obj as OntologyInfo);
	public override int GetHashCode() => ConceptsJson?.GetHashCode() ?? 0;
}

[Generator]
public sealed class AspectGenerator : IIncrementalGenerator {
	public void Initialize(IncrementalGeneratorInitializationContext context) {
		var aspectDeclarations = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				"DataCatalyst.Attributes.GameAspectAttribute",
				predicate: (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax,
				transform: GetAspectInfo)
			.Where(x => x is not null);

		var conceptDeclarations = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				"DataCatalyst.Attributes.GameConceptAttribute",
				predicate: (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax,
				transform: GetConceptInfo)
			.Where(x => x is not null);

		var jsonData = context.AdditionalTextsProvider
			.Where(f => Path.GetExtension(f.Path).Equals(".json", StringComparison.OrdinalIgnoreCase))
			.Select((t, _) => {
				var fName = Path.GetFileName(t.Path);
				if (fName.Equals("mods.json", StringComparison.OrdinalIgnoreCase)) return "";

				var content = t.GetText()?.ToString() ?? "";
				if (string.IsNullOrEmpty(content)) {
					return "";
				}

				try {
					using var d = JsonDocument.Parse(content);
					var r = d.RootElement;
					if (r.ValueKind != JsonValueKind.Object) {
						return "";
					}

					// Skip ontology files (content-detected, not filename-based)
					if ((r.TryGetProperty("concepts", out var ck) && ck.ValueKind == JsonValueKind.Object) ||
						(r.TryGetProperty("aspects", out var ak) && ak.ValueKind == JsonValueKind.Object) ||
						(r.TryGetProperty("relations", out var rk) && rk.ValueKind == JsonValueKind.Object)) {
						return "";
					}

					var beings = new List<string>();
					foreach (var beingProp in r.EnumerateObject()) {
						var beingKey = beingProp.Name;

						var beingObj = beingProp.Value;
						if (beingObj.ValueKind != JsonValueKind.Object) {
							continue;
						}

						var props = new List<string>();
						foreach (var prop in beingObj.EnumerateObject()) {
							if (prop.Name.Equals("$inherits", StringComparison.OrdinalIgnoreCase) ||
								prop.Name.Equals("inherits", StringComparison.OrdinalIgnoreCase)) {
								continue;
							}

							var nested = new List<string>();
							if (prop.Value.ValueKind == JsonValueKind.Object) {
								foreach (var nestedProp in prop.Value.EnumerateObject()) {
									nested.Add(nestedProp.Name);
								}
							}
							nested.Sort();
							props.Add($"{prop.Name}({string.Join(",", nested)})");
						}
						props.Sort();
						beings.Add($"{beingKey}[{string.Join(";", props)}]");
					}
					beings.Sort();
					return string.Join("|", beings);
				}
				catch {
					return "";
				}
			})
			.Where(s => !string.IsNullOrEmpty(s))
			.Collect()
			.Select((ar, _) => {
				var merged = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
				foreach (var fileContent in ar) {
					foreach (var beingPart in fileContent.Split('|')) {
						var bracketIdx = beingPart.IndexOf('[');
						if (bracketIdx <= 0) {
							continue;
						}

						var beingKey = beingPart.Substring(0, bracketIdx);
						var body = beingPart.Substring(bracketIdx + 1, beingPart.Length - bracketIdx - 2);

						if (!merged.TryGetValue(beingKey, out var propsDict)) {
							merged[beingKey] = propsDict = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
						}

						foreach (var propPart in body.Split(';')) {
							if (string.IsNullOrEmpty(propPart)) {
								continue;
							}

							var parenIdx = propPart.IndexOf('(');
							if (parenIdx <= 0) {
								continue;
							}

							var propName = propPart.Substring(0, parenIdx);
							var nestedStr = propPart.Substring(parenIdx + 1, propPart.Length - parenIdx - 2);

							if (!propsDict.TryGetValue(propName, out var hs)) {
								propsDict[propName] = hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
							}

							foreach (var n in nestedStr.Split([','], StringSplitOptions.RemoveEmptyEntries)) {
								hs.Add(n);
							}
						}
					}
				}

				var beingList = new List<string>();
				foreach (var beingKv in merged.OrderBy(x => x.Key)) {
					var propList = new List<string>();
					foreach (var propKv in beingKv.Value.OrderBy(x => x.Key)) {
						propList.Add($"{propKv.Key}({string.Join(",", propKv.Value.OrderBy(s => s))})");
					}
					beingList.Add($"{beingKv.Key}[{string.Join(";", propList)}]");
				}
				return new JsonDataInfo(string.Join("|", beingList));
			});

		// Read ontology: lightweight content detection, no parser needed at this stage
		var ontologyData = context.AdditionalTextsProvider
			.Collect()
			.Select((ar, _) => {
				var sb = new StringBuilder();
				foreach (var text in ar) {
					var content = text.GetText()?.ToString() ?? "";
					if (string.IsNullOrEmpty(content)) continue;
					var ext = Path.GetExtension(text.Path);
					// Lightweight ontology detection: try JSON for "concepts"/"aspects" keys
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
						catch { }
					}
					// XML: check for root <ontology> or <concepts> element
					else if (ext.Equals(".xml", StringComparison.OrdinalIgnoreCase)) {
						if (content.Contains("<concepts>") || content.Contains("<ontology>")) {
							if (sb.Length > 0) sb.Append('\0');
							sb.Append(content);
						}
					}
					// Other formats forwarded as-is; handler will try parsers
					else if (ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
						  || ext.Equals(".toml", StringComparison.OrdinalIgnoreCase)
						  || ext.Equals(".yml", StringComparison.OrdinalIgnoreCase)) {
						if (sb.Length > 0) sb.Append('\0');
						sb.Append(content);
					}
				}
				return new OntologyInfo(sb.ToString());
			});

		var combined = aspectDeclarations.Collect()
			.Combine(conceptDeclarations.Collect())
			.Combine(jsonData)
			.Combine(ontologyData)
			.Combine(context.CompilationProvider);

		context.RegisterSourceOutput(combined, (spc, input) => {
			var aspects = input.Left.Left.Left.Left;
			var concepts = input.Left.Left.Left.Right;
			var jsonData = input.Left.Left.Right;
			var ontologyData = input.Left.Right;
			var compilation = input.Right;

			if (aspects.Length == 0 && concepts.Length == 0
				&& string.IsNullOrEmpty(ontologyData?.ConceptsJson)) {
				return;
			}

			// Parse ontology.json if present
			var ontologyRequires = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			var ontologySuggests = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			var ontologyAspects = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
			var hasOntology = !string.IsNullOrEmpty(ontologyData?.ConceptsJson);

			if (hasOntology) {
				var parts = ontologyData!.ConceptsJson.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
				// Build parser list: built-in JSON fallback + external parsers from MSBuild config
				var parsers = new List<IOntologyParser> { new BuiltinJsonParser() };
				// Load external parsers from MSBuild property OntologyParsers (semicolon-separated assembly paths)
				try {
					if (compilation.Options is CSharpCompilationOptions csOpts) {
						// Try to read from analyzer config options
						foreach (var tree in compilation.SyntaxTrees) {
							if (tree.FilePath.Contains("AssemblyAttributes")) {
								// Skip — this is where we'd read global options
								break;
							}
						}
					}
				}
				catch { }
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

			var aspectDict = aspects.ToDictionary(a => a!.Name, a => a!, StringComparer.OrdinalIgnoreCase);
			var localAspectNames = new HashSet<string>(aspects.Select(a => a!.Name), StringComparer.OrdinalIgnoreCase);
			var aspectNames = new HashSet<string>(localAspectNames, StringComparer.OrdinalIgnoreCase);
			var conceptNames = new HashSet<string>(concepts.Select(c => c!.Name), StringComparer.OrdinalIgnoreCase);

			// Compute the union of all ontology concept names
			var allOntologyConcepts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (hasOntology) {
				foreach (var k in ontologyRequires.Keys) allOntologyConcepts.Add(k);
				foreach (var k in ontologySuggests.Keys) allOntologyConcepts.Add(k);
			}
			// Union for DC0001 check during data scan: known = C# concepts OR ontology concepts
			var knownConceptNames = new HashSet<string>(conceptNames, StringComparer.OrdinalIgnoreCase);
			if (hasOntology) {
				foreach (var cn in allOntologyConcepts) knownConceptNames.Add(cn);
			}

			// Collect framework aspect types from referenced assemblies (StateGroup, StateTransitions, etc.)
			var frameworkAspects = new List<AspectInfo?>();
			var conceptAttr = compilation.GetTypeByMetadataName("DataCatalyst.Attributes.GameConceptAttribute");
			var conceptInterface = compilation.GetTypeByMetadataName("DataCatalyst.IConcept");
			var aspectAttr = compilation.GetTypeByMetadataName("DataCatalyst.Attributes.GameAspectAttribute");

			foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols) {
				var name = assembly.Name;
				var isSystem = name.StartsWith("System") || name.StartsWith("Microsoft") || name == "mscorlib" || name == "netstandard" || name.StartsWith("Windows") || name.StartsWith("Mono");
				if (!isSystem) {
					FindConceptsInNamespace(assembly.GlobalNamespace, conceptAttr, conceptInterface, conceptNames);
					FindAspectsInNamespaceEx(assembly.GlobalNamespace, aspectAttr, aspectNames, frameworkAspects);
				}
			}

			// Rebuild knownConceptNames after assembly scan (may have added State, Sensor, etc.)
			knownConceptNames = new HashSet<string>(conceptNames, StringComparer.OrdinalIgnoreCase);
			if (hasOntology) {
				foreach (var cn in allOntologyConcepts) knownConceptNames.Add(cn);
			}

			var beingConcepts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			// conceptAspects: start with ontology requires, then augment with data-inferred
			var conceptAspects = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
			if (hasOntology) {
				foreach (var kv in ontologyRequires)
					conceptAspects[kv.Key] = new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase);
			}

			if (!string.IsNullOrEmpty(jsonData.BeingsString)) {
				foreach (var beingPart in jsonData.BeingsString.Split('|')) {
					if (string.IsNullOrEmpty(beingPart)) {
						continue;
					}

					var bracketIdx = beingPart.IndexOf('[');
					if (bracketIdx <= 0) {
						continue;
					}

					var beingKey = beingPart.Substring(0, bracketIdx);
					var body = beingPart.Substring(bracketIdx + 1, beingPart.Length - bracketIdx - 2);

					if (!beingConcepts.TryGetValue(beingKey, out var cList)) {
						beingConcepts[beingKey] = cList = [];
					}

					var allAspects = new List<string>();
					foreach (var propPart in body.Split(';')) {
						if (string.IsNullOrEmpty(propPart)) {
							continue;
						}

						var parenIdx = propPart.IndexOf('(');
						if (parenIdx <= 0) {
							continue;
						}

						var propName = propPart.Substring(0, parenIdx);
						var nestedStr = propPart.Substring(parenIdx + 1, propPart.Length - parenIdx - 2);

						if (propName.StartsWith("$")) {
							var conceptName = propName.Substring(1);
							if (knownConceptNames.Contains(conceptName)) {
								if (!cList.Contains(conceptName)) {
									cList.Add(conceptName);
								}

								// Always augment conceptAspects with data-inferred aspects
								if (!conceptAspects.TryGetValue(conceptName, out var aSet)) {
									conceptAspects[conceptName] = aSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
								}

								foreach (var a in nestedStr.Split([','], StringSplitOptions.RemoveEmptyEntries)) {
									aSet.Add(a);
								}
							}
							else {
								var diagnostic = Diagnostic.Create(
									new DiagnosticDescriptor(
										"DC0001",
										"Unknown Concept",
										$"Unknown concept '{conceptName}' specified with '$' prefix in being '{beingKey}'",
										"DataCatalyst",
										DiagnosticSeverity.Warning,
										isEnabledByDefault: true),
									Location.None);
								spc.ReportDiagnostic(diagnostic);
							}
						}
						else {
							allAspects.Add(propName);
						}
					}

					// Always augment being-level aspects to all concepts of the being
					foreach (var conceptName in cList) {
						if (!conceptAspects.TryGetValue(conceptName, out var aSet)) {
							conceptAspects[conceptName] = aSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
						}
						foreach (var aspect in allAspects) {
							aSet.Add(aspect);
						}
					}
				}
			}

			var mem = new List<MemberDeclarationSyntax>();
			var ini = new List<StatementSyntax>();
			var helperMethods = new List<MemberDeclarationSyntax>();

			// Generate concept marker structs from ontology.json for concepts without C# structs
			if (hasOntology) {
				foreach (var cn in allOntologyConcepts) {
					if (!conceptNames.Contains(cn)) {
						var structDecl = $"public struct {Sanitize(cn)} : global::DataCatalyst.IConcept {{ }}";
						var s = ParseMemberDeclaration(structDecl);
						if (s != null) mem.Add(s);
						conceptNames.Add(cn);
					}
				}
			}

			// Generate aspect structs from ontology.json for aspects without C# structs
			if (hasOntology && ontologyAspects.Count > 0) {
				foreach (var kv in ontologyAspects) {
					var aName = Sanitize(kv.Key);
					if (aspectDict.ContainsKey(kv.Key)) continue;

					var mems = new List<MemberDeclarationSyntax>();
					foreach (var field in kv.Value) {
						var fName = Sanitize(field.Key);
						var typeSyntax = ParseTypeName(MapTypeString(field.Value));
						mems.Add(
							PropertyDeclaration(typeSyntax, fName)
								.AddModifiers(Token(SyntaxKind.PublicKeyword))
								.AddAccessorListAccessors(
									AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
										.WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
									AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
										.WithSemicolonToken(Token(SyntaxKind.SemicolonToken))));
					}

					var structDecl = StructDeclaration(aName)
						.AddModifiers(Token(SyntaxKind.PublicKeyword))
						.AddAttributeLists(
							AttributeList(SingletonSeparatedList(
								Attribute(ParseName("DataCatalyst.Attributes.GameAspectAttribute")))))
						.AddMembers([.. mems]);

					mem.Add(structDecl);

					// Build AspectInfo for ontology-defined aspect
					aspectDict[kv.Key] = new AspectInfo(kv.Key, "DataCatalyst.Generated",
						string.Join(";", kv.Value.Select(f => $"{f.Key}:{MapTypeString(f.Value)}")));
					localAspectNames.Add(kv.Key);
					aspectNames.Add(kv.Key);
				}
			}

			// Build combined aspect list (C# [GameAspect] + ontology-generated + framework)
			var combinedAspects = new List<AspectInfo?>();
			combinedAspects.AddRange(aspects);
			foreach (var fa in frameworkAspects) {
				if (fa != null && !combinedAspects.Any(a => a?.Name == fa.Name)) {
					combinedAspects.Add(fa);
					if (!aspectDict.ContainsKey(fa.Name))
						aspectDict[fa.Name] = fa;
				}
			}
			if (hasOntology) {
				foreach (var kv in ontologyAspects) {
					if (aspectDict.TryGetValue(kv.Key, out var ai) && !combinedAspects.Any(a => a?.Name == kv.Key)) {
						combinedAspects.Add(ai);
					}
				}
			}

			// Register aspects and generate deserializers
			var deserIdx = 0;
			foreach (var asp in combinedAspects) {
				if (asp == null) continue;
				var nm = Sanitize(asp.Name);
				var fullTypeName = $"global::{asp.Namespace}.{nm}";

				ini.Add(ParseStatement($"global::DataCatalyst.Storage.AspectTypeRegistry.Register(typeof({fullTypeName}));\n")!);

				// Register field schema (replaces reflection at runtime)
				var propParts = asp.PropertiesString.Split([';'], StringSplitOptions.RemoveEmptyEntries);
				if (propParts.Length > 0) {
					var fieldEntries = string.Join(", ",
						propParts.Select(p => {
							var idx = p.IndexOf(':');
							return idx > 0
								? $"{{ \"{p.Substring(0, idx)}\", typeof({p.Substring(idx + 1)}) }}"
								: "";
						}).Where(e => e.Length > 0));
					if (fieldEntries.Length > 0)
						ini.Add(ParseStatement(
							$"global::DataCatalyst.Registry.AspectFieldRegistry.Register(\"{asp.Name}\", new global::System.Collections.Generic.Dictionary<string, global::System.Type> {{ {fieldEntries} }});\n")!);
				}

				var setters = new List<string>();
				foreach (var part in propParts) {
					var idx = part.IndexOf(':');
					if (idx <= 0) {
						continue;
					}

					var pName = part.Substring(0, idx);
					var pType = part.Substring(idx + 1);
					var isNullable = pType.EndsWith("?");
					var isValueType = pType.EndsWith("int") || pType.EndsWith("System.Int32") ||
									   pType.EndsWith("long") || pType.EndsWith("System.Int64") ||
									   pType.EndsWith("float") || pType.EndsWith("System.Single") ||
									   pType.EndsWith("double") || pType.EndsWith("System.Double") ||
									   pType.EndsWith("bool") || pType.EndsWith("System.Boolean");
					var defVal = (isValueType || isNullable) ? $"default({pType})" : $"default({pType})!";
					setters.Add($"    {pName} = __dict.TryGetValue(\"{pName}\", out var __v{deserIdx}_{pName}) && __v{deserIdx}_{pName} != null ? {Cast(pType, "__v" + deserIdx + "_" + pName, aspectNames, localAspectNames)} : {defVal}");
				}

				var helperName = $"__Deser_{nm}";
				helperMethods.Add(ParseMemberDeclaration(
					$"static {fullTypeName} {helperName}(object? __n) {{\n" +
					$"    if (!(__n is global::System.Collections.Generic.Dictionary<string, object?> __dict))\n" +
					$"        return new {fullTypeName}();\n" +
					$"    return new {fullTypeName} {{\n" +
					string.Join(",\n", setters) + "\n" +
					$"    }};\n" +
					"}")!);

				ini.Add(ParseStatement(
					$"global::DataCatalyst.Storage.AspectTypeRegistry.RegisterDeserializer(typeof({fullTypeName}), (object __n) => {helperName}(__n));\n")!);
				deserIdx++;
			}

			// Being structs
			foreach (var kv in beingConcepts) {
				var en = Sanitize(kv.Key);
				var ifaces = string.Join(", ", kv.Value.Select(c =>
					$"global::DataCatalyst.IBelongTo<global::DataCatalyst.Generated.{Sanitize(c)}>"));

				var baseDecl = string.IsNullOrEmpty(ifaces)
					? $"public record struct {en} : global::DataCatalyst.IBeing {{ }}"
					: $"public record struct {en} : global::DataCatalyst.IBeing, {ifaces} {{ }}";

				var s = ParseMemberDeclaration(baseDecl);
				if (s != null) {
					mem.Add(s);
				}

				var typeArgs = string.Join(", ", kv.Value.Select(c => $"typeof(global::DataCatalyst.Generated.{Sanitize(c)})"));
				ini.Add(ParseStatement(
					$"global::DataCatalyst.Registry.BeingRegistry.Register<global::DataCatalyst.Generated.{en}>({typeArgs});\n")!);
			}

			// Typed pools for concepts
			foreach (var kv in conceptAspects) {
				var cn = Sanitize(kv.Key);
				// Skip concepts without a C# struct (ontology-only, no [GameConcept] in code)
				if (!conceptNames.Contains(kv.Key)) continue;

				var fas = new List<(string, string)>();
				foreach (var a in kv.Value) {
					if (aspectDict.TryGetValue(a, out var aspInfo) && aspInfo != null) {
						fas.Add((Sanitize(a), $"global::{aspInfo!.Namespace}.{Sanitize(a)}"));
					}
				}

				if (fas.Count == 0) {
					continue;
				}

				var sn = $"{cn}Aspects";
				var fields = string.Join("\n", fas.Select(f => $"    public {f.Item2} {f.Item1};"));
				var takeCases = string.Join("\n        ", fas.Select(f =>
					$"if (typeof(T) == typeof({f.Item2})) return ref global::System.Runtime.CompilerServices.Unsafe.As<{f.Item2}, T>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in this.{f.Item1}));"));

				mem.Add(ParseMemberDeclaration($"public struct {sn} {{ {fields} public ref readonly T Take<T>() where T : struct {{ {takeCases} throw new global::System.ArgumentException($\"Aspect '{{typeof(T).Name}}' not found in {sn}\"); }} }}")!);

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
}}")!);
				ini.Add(ParseStatement(
					$"global::DataCatalyst.Registry.BeingRegistry.RegisterPool(typeof(global::DataCatalyst.Generated.{cn}), () => new {cn}Pool());\n")!);
			}

			// Register $requires/$suggests from ontology.json
			if (hasOntology) {
				foreach (var conceptName in allOntologyConcepts) {
					var req = ontologyRequires.TryGetValue(conceptName, out var r) ? r : [];
					var sug = ontologySuggests.TryGetValue(conceptName, out var s) ? s : [];
					var reqArr = req.Count > 0
						? $"new string[] {{ {string.Join(", ", req.Select(a => $"\"{a}\""))} }}"
						: "global::System.Array.Empty<string>()";
					var sugArr = sug.Count > 0
						? $"new string[] {{ {string.Join(", ", sug.Select(a => $"\"{a}\""))} }}"
						: "global::System.Array.Empty<string>()";
					ini.Add(ParseStatement(
						$"global::DataCatalyst.Registry.RequiresRegistry.Register(\"{conceptName}\", {reqArr}, {sugArr});\n")!);
				}
			}

			// SchemaGen class = ModuleInitializer + inline deserializer helpers
			if (helperMethods.Count > 0 || ini.Count > 0) {
				helperMethods.Insert(0, MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Init")
					.WithAttributeLists(SingletonList(AttributeList(SingletonSeparatedList(
						Attribute(ParseName("System.Runtime.CompilerServices.ModuleInitializer"))))))
					.WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword)))
					.WithBody(Block(ini)));

				mem.Add(ClassDeclaration("SchemaGen")
					.WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword)))
					.WithMembers(List(helperMethods)));
			}

			if (mem.Count == 0) {
				return;
			}

			var sourceText = "#nullable enable\n// <auto-generated/>\n" +
				CompilationUnit()
					.WithMembers(SingletonList<MemberDeclarationSyntax>(
						NamespaceDeclaration(ParseName("DataCatalyst.Generated"))
							.WithMembers(List(mem))))
					.NormalizeWhitespace().ToFullString();
			spc.AddSource("SchemaAspects.g.cs", SourceText.From(sourceText, Encoding.UTF8));
		});
	}

	private static AspectInfo? GetAspectInfo(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken token) {
		if (context.TargetSymbol is not INamedTypeSymbol symbol) {
			return null;
		}

		var name = symbol.Name;
		var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "DataCatalyst.Generated";

		var props = new List<string>();
		foreach (var member in symbol.GetMembers()) {
			if (member.IsStatic) {
				continue;
			}

			if (member.DeclaredAccessibility != Accessibility.Public) {
				continue;
			}

			if (member is IPropertySymbol prop) {
				if (prop.IsReadOnly) {
					continue;
				}

				var typeStr = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				props.Add($"{prop.Name}:{typeStr}");
			}
			else if (member is IFieldSymbol field) {
				if (field.IsConst) {
					continue;
				}

				var typeStr = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				props.Add($"{field.Name}:{typeStr}");
			}
		}
		props.Sort();
		return new AspectInfo(name, ns, string.Join(";", props));
	}

	private static ConceptInfo? GetConceptInfo(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken token) {
		if (context.TargetSymbol is not INamedTypeSymbol symbol) {
			return null;
		}

		var name = symbol.Name;
		var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "DataCatalyst.Generated";

		return new ConceptInfo(name, ns);
	}

	private static bool IsList(string type, out string innerType) {
		innerType = "";
		var idx = type.IndexOf("List<");
		if (idx >= 0 && type.EndsWith(">")) {
			var start = idx + 5;
			innerType = type.Substring(start, type.Length - start - 1).Trim();
			return true;
		}
		return false;
	}

	private static bool IsDictionary(string type, out string innerType) {
		innerType = "";
		var idx = type.IndexOf("Dictionary<");
		if (idx >= 0 && type.EndsWith(">")) {
			var comma = type.LastIndexOf(',');
			if (comma >= 0) {
				innerType = type.Substring(comma + 1, type.Length - comma - 2).Trim();
				return true;
			}
		}
		return false;
	}

	private static string MapTypeString(string type) {
		string clrName;
		if (type == "int") clrName = "System.Int32";
		else if (type == "long") clrName = "System.Int64";
		else if (type == "float") clrName = "System.Single";
		else if (type == "double") clrName = "System.Double";
		else if (type == "bool") clrName = "System.Boolean";
		else if (type == "string") clrName = "System.String";
		else if (type == "object") clrName = "System.Object";
		else if (type.StartsWith("List<")) {
			var inner = type.Substring(5, type.Length - 6);
			clrName = $"System.Collections.Generic.List<{MapTypeString(inner)}>";
		}
		else if (type.StartsWith("Dictionary<")) {
			var comma = type.IndexOf(',', 11);
			if (comma < 0) clrName = "System.Object";
			else {
				var k = type.Substring(11, comma - 11).Trim();
				var v = type.Substring(comma + 1, type.Length - comma - 2).Trim();
				clrName = $"System.Collections.Generic.Dictionary<{MapTypeString(k)}, {MapTypeString(v)}>";
			}
		}
		else if (type.Contains('.')) clrName = type;
		else clrName = $"DataCatalyst.Generated.{type}";
		return $"global::{clrName}";
	}

	private static string Cast(string type, string varName, HashSet<string> aspectNames, HashSet<string>? localAspectNames = null) {
		var cleanTypeName = type.TrimEnd('?');
		var endsInQuestion = type.EndsWith("?");

		if (cleanTypeName.EndsWith("int") || cleanTypeName.EndsWith("System.Int32")) {
			return endsInQuestion
				? $"({varName} != null ? global::System.Convert.ToInt32({varName}) : null)"
				: $"global::System.Convert.ToInt32({varName})";
		}

		if (cleanTypeName.EndsWith("long") || cleanTypeName.EndsWith("System.Int64")) {
			return endsInQuestion
				? $"({varName} != null ? global::System.Convert.ToInt64({varName}) : null)"
				: $"global::System.Convert.ToInt64({varName})";
		}

		if (cleanTypeName.EndsWith("float") || cleanTypeName.EndsWith("System.Single")) {
			return endsInQuestion
				? $"({varName} != null ? global::System.Convert.ToSingle({varName}) : null)"
				: $"global::System.Convert.ToSingle({varName})";
		}

		if (cleanTypeName.EndsWith("double") || cleanTypeName.EndsWith("System.Double")) {
			return endsInQuestion
				? $"({varName} != null ? global::System.Convert.ToDouble({varName}) : null)"
				: $"global::System.Convert.ToDouble({varName})";
		}

		if (cleanTypeName.EndsWith("bool") || cleanTypeName.EndsWith("System.Boolean")) {
			return endsInQuestion
				? $"({varName} != null ? global::System.Convert.ToBoolean({varName}) : null)"
				: $"global::System.Convert.ToBoolean({varName})";
		}

		if (cleanTypeName.EndsWith("string") || cleanTypeName.EndsWith("System.String")) {
			return endsInQuestion ? $"global::System.Convert.ToString({varName})" : $"global::System.Convert.ToString({varName})!";
		}

		if (type.EndsWith("System.Type") || type.EndsWith("Type") || type.EndsWith("System.Type?") || type.EndsWith("Type?")) {
			var isNullable = type.EndsWith("?");
			var fallback = isNullable ? "null" : "default(global::System.Type)!";
			return $"(global::System.Convert.ToString({varName}) is string __s_{varName} ? (global::System.Linq.Enumerable.FirstOrDefault(global::DataCatalyst.Registry.BeingRegistry.All, r => r.BeingType.Name.Equals(__s_{varName}, global::System.StringComparison.OrdinalIgnoreCase)).BeingType ?? {fallback}) : {fallback})";
		}

		if (type.Contains("DataCatalyst.Ref<") || type.Contains("Ref<")) {
			var refCleanType = type.TrimEnd('?');
			var isNullable = type.EndsWith("?");
			var fallback = isNullable ? "null" : $"default({refCleanType})";
			return $"(global::System.Convert.ToString({varName}) is string __s_{varName} && global::System.Linq.Enumerable.FirstOrDefault(global::DataCatalyst.Registry.BeingRegistry.All, r => r.BeingType.Name.Equals(__s_{varName}, global::System.StringComparison.OrdinalIgnoreCase)).BeingType is global::System.Type __t_{varName} ? new {refCleanType}(__t_{varName}) : {fallback})";
		}

		var cleanType = type.TrimEnd('?');
		var simpleName = cleanType.Substring(cleanType.LastIndexOf('.') + 1);

		if (IsList(cleanType, out var innerType)) {
			return type.EndsWith("?")
				? $"({varName} is global::System.Collections.Generic.IList<object?> __en_{varName} ? global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Select(__en_{varName}, __x => {Cast(innerType, "__x", aspectNames)})) : null)"
				: $"global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Select((global::System.Collections.Generic.IList<object?>){varName}, __x => {Cast(innerType, "__x", aspectNames)}))";
		}

		if (IsDictionary(cleanType, out var dictValueType)) {
			return type.EndsWith("?")
				? $"({varName} is global::System.Collections.Generic.IDictionary<string, object?> __dict_{varName} ? global::System.Linq.Enumerable.ToDictionary(__dict_{varName}, __de => __de.Key, __de => {Cast(dictValueType, "__de.Value", aspectNames)}, global::System.StringComparer.OrdinalIgnoreCase) : null)"
				: $"global::System.Linq.Enumerable.ToDictionary((global::System.Collections.Generic.IDictionary<string, object?>){varName}, __de => __de.Key, __de => {Cast(dictValueType, "__de.Value", aspectNames)}, global::System.StringComparer.OrdinalIgnoreCase)";
		}

		if (aspectNames.Contains(simpleName)) {
			return $"__Deser_{Sanitize(simpleName)}({varName})";
		}

		return type.EndsWith("?") ? $"({type}){varName}" : $"({type}){varName}!";
	}

	private static string Sanitize(string n) {
		if (string.IsNullOrEmpty(n)) {
			return "Unknown";
		}

		var c = n.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
		return new string(c).Length > 0 ? new string(c) : "_";
	}

	private static void FindConceptsInNamespace(INamespaceSymbol ns, INamedTypeSymbol? conceptAttr, INamedTypeSymbol? conceptInterface, HashSet<string> conceptNames) {
		foreach (var member in ns.GetMembers()) {
			if (member is INamespaceSymbol nestedNs) {
				FindConceptsInNamespace(nestedNs, conceptAttr, conceptInterface, conceptNames);
			}
			else if (member is INamedTypeSymbol typeSymbol) {
				if (IsConcept(typeSymbol, conceptAttr, conceptInterface)) {
					conceptNames.Add(typeSymbol.Name);
				}
			}
		}
	}

	private static void FindAspectsInNamespaceEx(INamespaceSymbol ns, INamedTypeSymbol? aspectAttr, HashSet<string> aspectNames, List<AspectInfo?> results) {
		foreach (var member in ns.GetMembers()) {
			if (member is INamespaceSymbol nestedNs)
				FindAspectsInNamespaceEx(nestedNs, aspectAttr, aspectNames, results);
			else if (member is INamedTypeSymbol typeSymbol && aspectAttr != null
				&& typeSymbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, aspectAttr))) {
				var name = typeSymbol.Name;
				var nsName = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "DataCatalyst";
				aspectNames.Add(name);
				var props = new List<string>();
				foreach (var m in typeSymbol.GetMembers()) {
					if (m.IsStatic || m.DeclaredAccessibility != Accessibility.Public) continue;
					if (m is IPropertySymbol prop && !prop.IsReadOnly)
						props.Add($"{prop.Name}:{prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
					else if (m is IFieldSymbol field && !field.IsConst)
						props.Add($"{field.Name}:{field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
				}
				props.Sort();
				results.Add(new AspectInfo(name, nsName, string.Join(";", props)));
			}
		}
	}

	private static void FindAspectsInNamespace(INamespaceSymbol ns, INamedTypeSymbol? aspectAttr, HashSet<string> aspectNames) {
		foreach (var member in ns.GetMembers()) {
			if (member is INamespaceSymbol nestedNs) {
				FindAspectsInNamespace(nestedNs, aspectAttr, aspectNames);
			}
			else if (member is INamedTypeSymbol typeSymbol) {
				if (aspectAttr != null && typeSymbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, aspectAttr))) {
					aspectNames.Add(typeSymbol.Name);
				}
			}
		}
	}

	private static bool IsConcept(INamedTypeSymbol typeSymbol, INamedTypeSymbol? conceptAttr, INamedTypeSymbol? conceptInterface) {
		if (conceptAttr != null && typeSymbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, conceptAttr))) {
			return true;
		}
		if (conceptInterface != null && typeSymbol.AllInterfaces.Any(it => SymbolEqualityComparer.Default.Equals(it, conceptInterface))) {
			return true;
		}
		return false;
	}
}
