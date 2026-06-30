namespace DataCatalyst.Pipeline;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Knowledge;
using DataCatalyst.Loader;
using DataCatalyst.Pipeline.Stages;
using DataCatalyst.Registry;
using DataCatalyst.Schema;

public sealed class Pipeline : IPipeline {
	internal List<DataSource> _sources = [];
	internal List<IBaker> _bakers = [];
	internal List<IPipelineStage> _stages = [];

	public SchemaRegistry Schema { get; } = new();
	public RegistrySet Registries { get; }

	public Pipeline(RegistrySet registries) {
		Registries = registries;
		_stages.AddRange(DefaultStages());
	}

	private static List<IPipelineStage> DefaultStages() => [
		new OntologyStage(), new LoadStage(), new SchemaStage(),
		new MergeStage(), new InheritStage(), new ResolveStage(),
		new ValidateStage(), new CrossRefStage(), new KnowledgeStage(),
		new BakeStage()
	];

	public static List<DataSource> TopoSort(IReadOnlyList<DataSource> sources) {
		var map = new Dictionary<string, DataSource>(StringComparer.OrdinalIgnoreCase);
		var deg = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var edges = new Dictionary<string, List<DataSource>>(StringComparer.OrdinalIgnoreCase);
		foreach (var s in sources) { map[s.Name] = s; deg.TryAdd(s.Name, 0); }
		foreach (var s in sources) {
			foreach (var d in s.DependsOn) {
				if (map.ContainsKey(d)) {
					if (!edges.TryGetValue(d, out var l)) {
						edges[d] = l = [];
					}

					l.Add(s);
					deg[s.Name] = deg.GetValueOrDefault(s.Name) + 1;
				}
			}
		}

		var q = new Queue<DataSource>();
		foreach (var s in sources) {
			if (deg.GetValueOrDefault(s.Name) == 0) {
				q.Enqueue(s);
			}
		}

		var r = new List<DataSource>(sources.Count);
		while (q.Count > 0) {
			var s = q.Dequeue();
			r.Add(s);
			if (edges.TryGetValue(s.Name, out var deps)) {
				foreach (var d in deps) {
					if (--deg[d.Name] == 0) {
						q.Enqueue(d);
					}
				}
			}
		}
		if (r.Count != sources.Count) {
			var cycled = sources.Where(s => !r.Contains(s)).Select(s => $"'{s.Name}'");
			throw new InvalidOperationException(
				$"Circular dependency detected between sources: {string.Join(", ", cycled)}");
		}

		return r;
	}

	public Knowledge? Run(out DiagnosticBag diagnostics) {
		_stages = [.. _stages.OrderBy(s => s.Order)];
		var ctx = new PipelineContext(Schema, _sources, _bakers, Registries);
		diagnostics = ctx.Diagnostics;

		foreach (var s in _stages) {
			if (!s.Execute(ctx)) {
				break;
			}
		}

		Registries.Freeze();
		return ctx.Diagnostics.HasErrors ? null : ctx.Knowledge;
	}
}
