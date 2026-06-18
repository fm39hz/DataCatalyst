namespace DataCatalyst;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator]
public sealed class PrimitiveDiscoveryGenerator : IIncrementalGenerator {
	private static readonly DiagnosticDescriptor StructRecommendedWarning = new(
		id: "DC001",
		title: "DataComponent should be a struct",
		messageFormat: "[DataComponent] on '{0}' should be a struct for optimal AOT compatibility",
		category: "DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor CollisionWarning = new(
		id: "DC002",
		title: "Discriminator collision",
		messageFormat: "[DataComponent] types with name '{0}' collide. Use fully-qualified namespace in JSON to distinguish.",
		category: "DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor CycleWarning = new(
		id: "DC003",
		title: "Circular plugin dependency",
		messageFormat: "Circular dependency detected involving plugin '{0}'",
		category: "DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private const string DataComponentAttr = "DataCatalyst.Abstractions.DataComponentAttribute";
	private const string DataPluginAttr = "DataCatalyst.Abstractions.DataPluginAttribute";
	private const string DataPluginIface = "DataCatalyst.Abstractions.IDataPlugin";

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		// Discover [DataComponent] structs/classes in the compiling assembly only.
		// Each assembly self-registers via its own ModuleInitializer — cross-assembly scan is redundant.
		var primitives = context.SyntaxProvider.ForAttributeWithMetadataName(
			DataComponentAttr,
			static (node, _) => node is TypeDeclarationSyntax,
			static (ctx, _) => {
				var t = (INamedTypeSymbol)ctx.TargetSymbol;
				Location? warning = t.TypeKind != TypeKind.Structure
					? ctx.TargetNode.GetLocation()
					: null;
				var fullType = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				return new PrimitiveResult(fullType, fullType, warning);
			}).Collect();

		// Discover [DataPlugin] classes in the compiling assembly
		var plugins = context.SyntaxProvider.ForAttributeWithMetadataName(
			DataPluginAttr,
			static (node, _) => node is ClassDeclarationSyntax,
			static (ctx, _) => {
				var t = (INamedTypeSymbol)ctx.TargetSymbol;
				if (!t.AllInterfaces.Any(i => i.ToDisplayString() == DataPluginIface)) return default((string, string, string[])?);
				var fullType = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var deps = GetDeps(t.GetAttributes());
				return ((string FullType, string Id, string[] Deps)?)(fullType, t.Name, deps);
			})
			.Where(static p => p is not null)
			.Select(static (p, _) => p!.Value)
			.Collect();

		context.RegisterSourceOutput(
			context.CompilationProvider.Combine(plugins).Combine(primitives),
			static (spc, payload) => {
				var ((comp, pl), pr) = payload;

				// Report non-struct warnings
				foreach (var p in pr) {
					if (p.Warning is {} w)
						spc.ReportDiagnostic(Diagnostic.Create(StructRecommendedWarning, w));
				}

				Emit(spc, pl, pr);
			});
	}

	private static PrimitiveResult MakePrimitiveResult(INamedTypeSymbol t, Location? warning) {
		var fullType = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		return new PrimitiveResult(fullType, fullType, warning);
	}

	private readonly struct PrimitiveResult {
		public readonly string? FullType;   // "global::Game.Health"
		public readonly string? Discrim;    // "Game.Health" (JSON key)
		public readonly Location? Warning;

		public PrimitiveResult(string? fullType, string? discrim, Location? warning) {
			FullType = fullType;
			Discrim = discrim;
			Warning = warning;
		}
	}

	private static string[] GetDeps(ImmutableArray<AttributeData> attrs) {
		foreach (var a in attrs) {
			if (a.AttributeClass == null || a.AttributeClass.ToDisplayString() != DataPluginAttr) continue;
			foreach (var n in a.NamedArguments) {
				if (n.Key == "DependsOn" && n.Value.Values is { Length: > 0 } vs) {
					var list = new List<string>(vs.Length);
					foreach (var v in vs) {
						if (v.Value is INamedTypeSymbol dt)
							list.Add(dt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
					}
					return [.. list];
				}
			}
		}
		return [];
	}

	private static string Discriminator(string fullType) {
		// strip "global::" prefix, replace "+" (nested) with "."
		var s = fullType.StartsWith("global::") ? fullType.Substring(8) : fullType;
		return s.Replace('+', '.');
	}

	private static void Emit(SourceProductionContext spc,
		ImmutableArray<(string FullType, string Id, string[] Deps)> allPlugins,
		ImmutableArray<PrimitiveResult> allPrims) {

		// Filter non-null primitives and compute discriminator keys
		var prims = new List<(string FullType, string Discrim)>();
		var seenDiscrims = new HashSet<string>();
		var colliding = new HashSet<string>();

		foreach (var p in allPrims) {
			if (p.FullType == null) continue;
			var d = p.Discrim ?? Discriminator(p.FullType);
			if (!seenDiscrims.Add(d)) {
				// Collision — switch both colliding entries to full namespace
				colliding.Add(d);
				// Need to find the previous entry and update it
			}
			prims.Add((p.FullType, d));
		}

		// Resolve collisions: entries with colliding short keys get full namespace path
		if (colliding.Count > 0) {
			foreach (var d in colliding) {
				spc.ReportDiagnostic(Diagnostic.Create(CollisionWarning, Location.None, d));
			}
			for (var i = 0; i < prims.Count; i++) {
				var (ft, d) = prims[i];
				if (colliding.Contains(d)) {
					prims[i] = (ft, Discriminator(ft));
				}
			}
		}

		if (allPlugins.Length == 0 && prims.Count == 0) return;

		var sb = new StringBuilder();
		sb.Append("// <auto-generated/>\n#nullable enable\n\nnamespace DataCatalyst.Core {\n");
		sb.Append("\tpublic static partial class PrimitiveRegistrations {\n");

		sb.Append("\t\t[System.Runtime.CompilerServices.ModuleInitializer]\n");
		sb.Append("\t\tinternal static void Init() {\n");

		if (allPlugins.Length > 0) {
			var sorted = TopoSort(allPlugins.ToList(), spc);
			foreach (var (ft, _, _) in sorted) {
				sb.Append("\t\t\tglobal::DataCatalyst.Core.PluginRegistry.Register<")
					.Append(ft).AppendLine(">();");
			}
		}

		if (prims.Count > 0) {
			foreach (var (ft, _) in prims) {
				sb.Append("\t\t\tglobal::DataCatalyst.Core.PrimitiveRegistry.Register<")
					.Append(ft).AppendLine(">();");
			}

			sb.AppendLine();
			sb.Append("\t\t\tglobal::DataCatalyst.Core.PrimitiveRegistry.RegisterIds(new() {\n");
			foreach (var (ft, d) in prims) {
				sb.Append("\t\t\t\t{ \"").Append(d).Append("\", typeof(").Append(ft).Append(") },")
					.Append(" // ").AppendLine(d);
			}
			sb.Append("\t\t\t});\n");
		}

		sb.Append("\t\t}\n");

		sb.Append("\n\t\tpublic static void RegisterTo(global::DataCatalyst.Core.DataRegistry registry) {\n");
		if (allPlugins.Length > 0) {
			var sorted = TopoSort(allPlugins.ToList(), spc);
			foreach (var (ft, _, _) in sorted) {
				sb.Append("\t\t\tregistry.RegisterPlugin<").Append(ft).AppendLine(">();");
			}
		}
		if (prims.Count > 0) {
			foreach (var (ft, _) in prims) {
				sb.Append("\t\t\tregistry.RegisterComponent<").Append(ft).AppendLine(">();");
			}
		}
		sb.Append("\t\t}\n");
		sb.Append("\t}\n}");

		spc.AddSource("PrimitiveRegistrations.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private static List<(string FullType, string Id, string[] Deps)> TopoSort(
		List<(string FullType, string Id, string[] Deps)> plugins,
		SourceProductionContext spc) {

		var map = new Dictionary<string, (string FullType, string Id, string[] Deps)>();
		var indeg = new Dictionary<string, int>();
		var edges = new Dictionary<string, List<string>>();

		foreach (var p in plugins) {
			if (map.ContainsKey(p.FullType)) continue; // dedup
			map[p.FullType] = p;
			indeg[p.FullType] = 0;
			edges[p.FullType] = [];
		}

		foreach (var p in plugins) {
			if (!map.ContainsKey(p.FullType)) continue;
			foreach (var d in p.Deps) {
				if (map.ContainsKey(d)) {
					edges[d].Add(p.FullType);
					indeg[p.FullType]++;
				}
			}
		}

		var ready = new Queue<string>();
		foreach (var kv in map) {
			if (indeg.TryGetValue(kv.Key, out var d) && d == 0) ready.Enqueue(kv.Key);
		}

		var result = new List<(string FullType, string Id, string[] Deps)>();
		while (ready.Count > 0) {
			var cur = ready.Dequeue();
			result.Add(map[cur]);
			if (edges.TryGetValue(cur, out var list)) {
				foreach (var c in list) {
					if (--indeg[c] == 0 && map.ContainsKey(c)) ready.Enqueue(c);
				}
			}
		}

		// Report cycles — remaining nodes with indeg > 0 are in a cycle
		foreach (var kv in map) {
			if (!result.Any(r => r.FullType == kv.Key)) {
				spc.ReportDiagnostic(Diagnostic.Create(CycleWarning, Location.None, kv.Key));
			}
		}

		// Append cyclic plugins after sorted ones (still emit, but flagged)
		foreach (var p in plugins) {
			if (!result.Any(r => r.FullType == p.FullType)) result.Add(p);
		}

		return result;
	}
}
