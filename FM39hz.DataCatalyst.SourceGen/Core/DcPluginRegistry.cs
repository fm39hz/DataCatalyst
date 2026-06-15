namespace FM39hz.DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using FM39hz.DataCatalyst.Abstractions;

/// <summary>Static registry of every DataCatalyst plugin. Plugins self-register via [ModuleInitializer].</summary>
public static class DcPluginRegistry {
	private static readonly List<PluginEntry<IEntryPointReader>> _readers = [];
	private static readonly List<PluginEntry<IPrimitiveTypeRule>> _primitives = [];
	private static readonly List<PluginEntry<ISchemaProvider>> _schemaProviders = [];
	private static readonly List<PluginEntry<ITypeEmitter>> _emitters = [];
	private static readonly List<PluginEntry<IDcPostProcessor>> _postProcessors = [];
	private static readonly List<PluginEntry<ITemplateLiteralRule>> _templateLiterals = [];
	private static readonly List<PluginEntry<ITypeEmitter>> _companionEmitters = [];
	private static int _sequence;

	public static void Register(IEntryPointReader plugin, params Type[] dependsOn) => _readers.Add(CreateEntry(plugin, dependsOn));
	public static void Register(IPrimitiveTypeRule plugin, params Type[] dependsOn) => _primitives.Add(CreateEntry(plugin, dependsOn));
	public static void Register(ISchemaProvider plugin, params Type[] dependsOn) => _schemaProviders.Add(CreateEntry(plugin, dependsOn));
	public static void Register(ITypeEmitter plugin, params Type[] dependsOn) => _emitters.Add(CreateEntry(plugin, dependsOn));
	public static void Register(IDcPostProcessor plugin, params Type[] dependsOn) => _postProcessors.Add(CreateEntry(plugin, dependsOn));
	public static void Register(ITemplateLiteralRule plugin, params Type[] dependsOn) => _templateLiterals.Add(CreateEntry(plugin, dependsOn));
	public static void RegisterCompanion(ITypeEmitter plugin, params Type[] dependsOn) => _companionEmitters.Add(CreateEntry(plugin, dependsOn));

	public static IReadOnlyList<IEntryPointReader> Readers => Order(_readers);
	public static IReadOnlyList<IPrimitiveTypeRule> Primitives => Order(_primitives);
	public static IReadOnlyList<ISchemaProvider> SchemaProviders => Order(_schemaProviders);
	public static IReadOnlyList<ITypeEmitter> Emitters => Order(_emitters);
	public static IReadOnlyList<IDcPostProcessor> PostProcessors => Order(_postProcessors);
	public static IReadOnlyList<ITemplateLiteralRule> TemplateLiteralRules => Order(_templateLiterals);
	public static IReadOnlyList<ITypeEmitter> CompanionEmitters => Order(_companionEmitters);

	private static int NextSequence() => _sequence++;

	private static PluginEntry<TPlugin> CreateEntry<TPlugin>(TPlugin plugin, Type[] dependsOn) {
		var dependencies = new HashSet<string>(StringComparer.Ordinal);
		foreach (var dependency in dependsOn) {
			if (!string.IsNullOrWhiteSpace(dependency.FullName)) {
				dependencies.Add(dependency.FullName!);
			}
		}
		return new PluginEntry<TPlugin>(plugin, dependencies, NextSequence());
	}

	private static IReadOnlyList<TPlugin> Order<TPlugin>(List<PluginEntry<TPlugin>> entries) {
		var map = new Dictionary<string, PluginEntry<TPlugin>>(StringComparer.Ordinal);
		foreach (var entry in entries) {
			if (string.IsNullOrWhiteSpace(entry.TypeName)) continue;
			map[entry.TypeName] = entry;
		}

		var indegree = new Dictionary<string, int>(map.Count, StringComparer.Ordinal);
		var outgoing = new Dictionary<string, List<string>>(map.Count, StringComparer.Ordinal);
		foreach (var name in map.Keys) {
			indegree[name] = 0;
			outgoing[name] = [];
		}

		foreach (var pair in map) {
			var name = pair.Key;
			var entry = pair.Value;
			foreach (var dependency in entry.Dependencies) {
				if (!map.ContainsKey(dependency)) continue;
				indegree[name]++;
				outgoing[dependency].Add(name);
			}
		}

		var ready = indegree
			.Where(static pair => pair.Value == 0)
			.Select(pair => map[pair.Key])
			.OrderBy(static entry => entry.TypeName, StringComparer.Ordinal)
			.ThenBy(static entry => entry.Sequence)
			.ToList();

		var ordered = new List<TPlugin>(map.Count);
		while (ready.Count > 0) {
			var current = ready[0];
			ready.RemoveAt(0);
			ordered.Add(current.Plugin);

			foreach (var child in outgoing[current.TypeName]) {
				indegree[child]--;
				if (indegree[child] == 0) {
					ready.Add(map[child]);
				}
			}

			ready.Sort(static (a, b) => {
				var byName = StringComparer.Ordinal.Compare(a.TypeName, b.TypeName);
				return byName != 0 ? byName : a.Sequence.CompareTo(b.Sequence);
			});
		}

		if (ordered.Count != map.Count) {
			var cycle = string.Join(", ", indegree.Where(static pair => pair.Value > 0).Select(static pair => pair.Key));
			throw new InvalidOperationException("DataCatalyst plugin dependency cycle detected: " + cycle);
		}

		return ordered;
	}

	private readonly struct PluginEntry<TPlugin>(TPlugin plugin, HashSet<string> dependencies, int sequence) {
		public TPlugin Plugin { get; } = plugin;
		public string TypeName { get; } = plugin?.GetType().FullName ?? string.Empty;
		public HashSet<string> Dependencies { get; } = dependencies;
		public int Sequence { get; } = sequence;
	}
}
