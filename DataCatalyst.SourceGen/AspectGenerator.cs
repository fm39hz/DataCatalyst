namespace DataCatalyst.V2;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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

public sealed class JsonDataInfo(string entriesString) : IEquatable<JsonDataInfo> {
	public string EntriesString { get; } = entriesString;

	public bool Equals(JsonDataInfo? other) {
		if (other is null) {
			return false;
		}

		return EntriesString == other.EntriesString;
	}

	public override bool Equals(object? obj) => Equals(obj as JsonDataInfo);

	public override int GetHashCode() => EntriesString?.GetHashCode() ?? 0;
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

					var entries = new List<string>();
					foreach (var entryProp in r.EnumerateObject()) {
						var entryKey = entryProp.Name;
						if (entryKey.Equals("$mapping", StringComparison.OrdinalIgnoreCase) ||
							entryKey.Equals("$concepts", StringComparison.OrdinalIgnoreCase) ||
							entryKey.Equals("$aspects", StringComparison.OrdinalIgnoreCase) ||
							entryKey.Equals("$entries", StringComparison.OrdinalIgnoreCase)) {
							continue;
						}

						var entryObj = entryProp.Value;
						if (entryObj.ValueKind != JsonValueKind.Object) {
							continue;
						}

						var props = new List<string>();
						foreach (var prop in entryObj.EnumerateObject()) {
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
						entries.Add($"{entryKey}[{string.Join(";", props)}]");
					}
					entries.Sort();
					return string.Join("|", entries);
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
					foreach (var entryPart in fileContent.Split('|')) {
						var bracketIdx = entryPart.IndexOf('[');
						if (bracketIdx <= 0) {
							continue;
						}

						var entryKey = entryPart.Substring(0, bracketIdx);
						var body = entryPart.Substring(bracketIdx + 1, entryPart.Length - bracketIdx - 2);

						if (!merged.TryGetValue(entryKey, out var propsDict)) {
							merged[entryKey] = propsDict = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
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

				var entryList = new List<string>();
				foreach (var entryKv in merged.OrderBy(x => x.Key)) {
					var propList = new List<string>();
					foreach (var propKv in entryKv.Value.OrderBy(x => x.Key)) {
						propList.Add($"{propKv.Key}({string.Join(",", propKv.Value.OrderBy(s => s))})");
					}
					entryList.Add($"{entryKv.Key}[{string.Join(";", propList)}]");
				}
				return new JsonDataInfo(string.Join("|", entryList));
			});

		var combined = aspectDeclarations.Collect()
			.Combine(conceptDeclarations.Collect())
			.Combine(jsonData);

		context.RegisterSourceOutput(combined, (spc, input) => {
			var aspects = input.Left.Left;
			var concepts = input.Left.Right;
			var jsonData = input.Right;

			if (aspects.Length == 0 && concepts.Length == 0) {
				return;
			}

			var aspectDict = aspects.ToDictionary(a => a!.Name, a => a!, StringComparer.OrdinalIgnoreCase);
			var aspectNames = new HashSet<string>(aspects.Select(a => a!.Name), StringComparer.OrdinalIgnoreCase);
			var conceptNames = new HashSet<string>(concepts.Select(c => c!.Name), StringComparer.OrdinalIgnoreCase);

			var entryConcepts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			var conceptAspects = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

			if (!string.IsNullOrEmpty(jsonData.EntriesString)) {
				foreach (var entryPart in jsonData.EntriesString.Split('|')) {
					if (string.IsNullOrEmpty(entryPart)) {
						continue;
					}

					var bracketIdx = entryPart.IndexOf('[');
					if (bracketIdx <= 0) {
						continue;
					}

					var entryKey = entryPart.Substring(0, bracketIdx);
					var body = entryPart.Substring(bracketIdx + 1, entryPart.Length - bracketIdx - 2);

					if (!entryConcepts.TryGetValue(entryKey, out var cList)) {
						entryConcepts[entryKey] = cList = [];
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

						if (propName.StartsWith("$")) {
							var conceptName = propName.Substring(1);
							if (conceptNames.Contains(conceptName)) {
								if (!cList.Contains(conceptName)) {
									cList.Add(conceptName);
								}

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
										$"Unknown concept '{conceptName}' specified with '$' prefix in entry '{entryKey}'",
										"DataCatalyst",
										DiagnosticSeverity.Warning,
										isEnabledByDefault: true),
									Location.None);
								spc.ReportDiagnostic(diagnostic);
							}
						}
					}
				}
			}

			var mem = new List<MemberDeclarationSyntax>();
			var ini = new List<StatementSyntax>();
			var helperMethods = new List<MemberDeclarationSyntax>();

			// Register aspects and generate deserializers
			var deserIdx = 0;
			foreach (var asp in aspects) {
				var nm = Sanitize(asp!.Name);
				var fullTypeName = $"global::{asp.Namespace}.{nm}";

				ini.Add(ParseStatement($"global::DataCatalyst.Storage.AspectTypeRegistry.Register(typeof({fullTypeName}));\n")!);

				var propParts = asp.PropertiesString.Split([';'], StringSplitOptions.RemoveEmptyEntries);
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
					setters.Add($"    {pName} = __dict.TryGetValue(\"{pName}\", out var __v{deserIdx}_{pName}) && __v{deserIdx}_{pName} != null ? {Cast(pType, "__v" + deserIdx + "_" + pName, aspectNames)} : {defVal}");
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

			// Entry structs
			foreach (var kv in entryConcepts) {
				var en = Sanitize(kv.Key);
				var ifaces = string.Join(", ", kv.Value.Select(c =>
					$"global::DataCatalyst.IBelongTo<global::DataCatalyst.Generated.{Sanitize(c)}>"));

				var baseDecl = string.IsNullOrEmpty(ifaces)
					? $"public record struct {en} : global::DataCatalyst.IEntry {{ }}"
					: $"public record struct {en} : global::DataCatalyst.IEntry, {ifaces} {{ }}";

				var s = ParseMemberDeclaration(baseDecl);
				if (s != null) {
					mem.Add(s);
				}

				var typeArgs = string.Join(", ", kv.Value.Select(c => $"typeof(global::DataCatalyst.Generated.{Sanitize(c)})"));
				ini.Add(ParseStatement(
					$"global::DataCatalyst.Registry.EntryRegistry.Register<global::DataCatalyst.Generated.{en}>({typeArgs});\n")!);
			}

			// Typed pools for concepts
			foreach (var kv in conceptAspects) {
				var cn = Sanitize(kv.Key);
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
					$"global::DataCatalyst.Registry.EntryRegistry.RegisterPool(typeof(global::DataCatalyst.Generated.{cn}), () => new {cn}Pool());\n")!);
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

	private static string Cast(string type, string varName, HashSet<string> aspectNames) {
		if (type.EndsWith("int") || type.EndsWith("System.Int32")) {
			return $"global::System.Convert.ToInt32({varName})";
		}

		if (type.EndsWith("long") || type.EndsWith("System.Int64")) {
			return $"global::System.Convert.ToInt64({varName})";
		}

		if (type.EndsWith("float") || type.EndsWith("System.Single")) {
			return $"global::System.Convert.ToSingle({varName})";
		}

		if (type.EndsWith("double") || type.EndsWith("System.Double")) {
			return $"global::System.Convert.ToDouble({varName})";
		}

		if (type.EndsWith("bool") || type.EndsWith("System.Boolean")) {
			return $"global::System.Convert.ToBoolean({varName})";
		}

		if (type.EndsWith("string") || type.EndsWith("System.String")) {
			return type.EndsWith("?") ? $"global::System.Convert.ToString({varName})" : $"global::System.Convert.ToString({varName})!";
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
}
