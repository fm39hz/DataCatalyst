namespace DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Abstractions;

/// <summary>
/// Fluent API for composing multiple data sources and coordinating the data build pipeline.
/// </summary>
public class DataPipeline {
	private readonly DataCatalystEnvironment _env;
	private readonly List<DataSource> _sources = [];
	private readonly List<string> _diagnostics = [];
	private int _legacyPriority = 0;

	public DataPipeline(DataCatalystEnvironment? env = null) {
		_env = env ?? new DataCatalystEnvironment();
		_env.Plugins.Register<GameConceptPlugin>();
	}

	/// <summary>
	/// Legacy API: Loads all data entries from a directory using the specified loader.
	/// </summary>
	public DataPipeline Load(IDataLoader loader, string path) {
		var source = new DataSource($"__legacy_{_sources.Count}", loader, path) {
			Priority = _legacyPriority++,
			MergePolicy = MergePolicy.Patch
		};
		_sources.Add(source);
		return this;
	}

	/// <summary>
	/// New API: Adds a structured, prioritized data source configuration to the pipeline.
	/// </summary>
	public DataPipeline AddSource(DataSource source) {
#if NET6_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(source);
#else
		if (source == null) throw new ArgumentNullException(nameof(source));
#endif
		_sources.Add(source);
		return this;
	}

	/// <summary>
	/// Coordinates the multi-stage pipeline:
	/// 1. Sắp xếp các nguồn bằng Kahn's topological sort.
	/// 2. Load các tệp dữ liệu theo nguồn và kích hoạt IPerSourcePlugin.
	/// 3. Kích hoạt IPostLoadPlugin trên toàn bộ entries thu thập được.
	/// 4. Dựng DataGraph và OverlayGraph qua PolicyGraphBuilder.
	/// 5. Kích hoạt IGraphPlugin.
	/// 6. Phân rã cây kế thừa qua DataCatalogBuilder (DFS + Memoization).
	/// 7. Kích hoạt ICatalogPlugin.
	/// 8. Áp dụng Overlays (Localization/Debug) sau cùng.
	/// </summary>
	public DataCatalog Build() {
		// Stage 0: Topological Sort on Sources
		var sortedSources = LoadOrderResolver.Resolve(_sources, _diagnostics);

		// Stage 1: Load entries per source & run PerSource Plugins
		var entriesWithSource = new List<(DataEntry Entry, DataSource Source)>();
		foreach (var source in sortedSources) {
			var result = source.Loader.LoadDirectory(source.Path);
			_diagnostics.AddRange(result.Diagnostics);

			var sourceEntries = result.Entries.ToList();

			// IPerSourcePlugin hook
			var perSourcePlugins = _env.Plugins.EnabledPlugins.OfType<IPerSourcePlugin>().ToList();
			foreach (var plugin in perSourcePlugins) {
				try {
					plugin.OnSourceLoaded(source, sourceEntries, _diagnostics);
				}
				catch (Exception ex) {
					_diagnostics.Add($"[Error] Plugin '{plugin.GetType().Name}' failed in OnSourceLoaded: {ex.Message}");
				}
			}

			foreach (var entry in sourceEntries) {
				entry.SourceFile = $"{source.Name}::{entry.SourceFile ?? "unknown"}";
				entriesWithSource.Add((entry, source));
			}
		}

		// Stage 2: PostLoad Hook on all entries
		var allEntries = entriesWithSource.Select(x => x.Entry).ToList();
		var postLoadPlugins = _env.Plugins.EnabledPlugins.OfType<IPostLoadPlugin>().ToList();
		foreach (var plugin in postLoadPlugins) {
			try {
				plugin.OnEntriesLoaded(allEntries, _diagnostics);
			}
			catch (Exception ex) {
				_diagnostics.Add($"[Error] Plugin '{plugin.GetType().Name}' failed in OnEntriesLoaded: {ex.Message}");
			}
		}

		// Stage 3: Policy-based deduplication and merging
		var (graph, overlay) = PolicyGraphBuilder.Build(entriesWithSource, _diagnostics);

		// Stage 4: Graph hooks
		var graphPlugins = _env.Plugins.EnabledPlugins.OfType<IGraphPlugin>().ToList();
		foreach (var plugin in graphPlugins) {
			try {
				plugin.OnGraphBuilt(graph, _diagnostics);
			}
			catch (Exception ex) {
				_diagnostics.Add($"[Error] Plugin '{plugin.GetType().Name}' failed in OnGraphBuilt: {ex.Message}");
			}
		}

		// Stage 5 & 6: Resolve inheritance & run Catalog plugins (contained inside DataCatalogBuilder)
		var catalog = DataCatalogBuilder.Resolve(graph, _diagnostics, _env);

		// Stage 7: Apply Overlays
		overlay.ApplyTo(catalog);

		return catalog;
	}

	/// <summary>
	/// Diagnostics warnings/info/errors collected during pipeline run.
	/// </summary>
	public IReadOnlyList<string> Diagnostics => _diagnostics;
}
