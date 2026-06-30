namespace Catalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;
using Catalyst.Knowledge;
using Catalyst.Storage;

public sealed class BakeStage : IPipelineStage {
	public int Order => 100;
	public bool Execute(PipelineContext ctx) {
		var bctx = (IBakeContext)ctx;
		var rctx = (IResolveContext)ctx;
		if (bctx.Knowledge == null || rctx.Resolved == null || bctx.Bakers.Count == 0) return true;

		var cache = new Dictionary<Type, Dictionary<string, object>>();
		var bakerBySource = new Dictionary<Type, IBaker>();
		foreach (var baker in bctx.Bakers) {
			bakerBySource[baker.SourceAspectType] = baker;
		}

		foreach (var being in rctx.Resolved) {
			foreach (var kv in being.Components) {
				if (bakerBySource.TryGetValue(kv.Key, out var baker)) {
					if (!cache.TryGetValue(baker.BakedType, out var inner)) {
						cache[baker.BakedType] = inner = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
					}
					try {
						var b = baker.Bake(being.Key, kv.Value, bctx.Knowledge, bctx.Diagnostics);
						if (b != null) inner[being.Key] = b;
					}
					catch (Exception ex) {
						bctx.Diagnostics.Error($"Baker failed for '{being.Key}': {ex.Message}");
					}
				}
			}
		}

		if (cache.Count == 0) return true;

		bctx.Knowledge = new Knowledge(
			new Dictionary<Type, ITypedStoragePool>(bctx.Knowledge.Pools),
			new Dictionary<Type, int>(bctx.Knowledge.BeingIndices),
			bctx.Knowledge.Schema, cache);
		return true;
	}
}
