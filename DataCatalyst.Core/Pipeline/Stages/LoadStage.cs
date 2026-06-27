namespace DataCatalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;
using DataCatalyst.Loader;
using DataCatalyst.Storage;

public sealed class LoadStage : IPipelineStage {
	public int Order => 10;

	public bool Execute(PipelineContext ctx) {
		var lctx = (ILoadContext)ctx;
		List<DataSource> sorted;
		try {
			sorted = Pipeline.TopoSort(lctx.Sources);
		}
		catch (InvalidOperationException ex) {
			lctx.Diagnostics.Error(ex.Message);
			return false;
		}
		var raw = new System.Collections.Generic.List<RawBeing>();

		foreach (var source in sorted) {
			var result = source.Loader.LoadDirectory(source.Path);
			foreach (var d in result.Diagnostics) {
				lctx.Diagnostics.Warn(d);
			}

			foreach (var b in result.Beings) {
				if (b is RawBeing rb) {
					rb.MergePolicyValue = source.MergePolicy;
					raw.Add(rb);
					lctx.Keys.Add(rb.Key);
				}
			}
			foreach (var kv in result.Mappings) {
				if (!lctx.Mappings.TryGetValue(kv.Key, out var list)) {
					lctx.Mappings[kv.Key] = list = [];
				}

				foreach (var v in kv.Value) {
					if (!list.Contains(v)) {
						list.Add(v);
					}
				}
			}
		}

		for (var i = 0; i < raw.Count; i++) {
			raw[i].AssignedIndex = i;
		}

		lctx.Raw = raw;
		return !lctx.Diagnostics.HasErrors;
	}
}
