namespace DataCatalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Storage;

public sealed class MergeStage : IPipelineStage {
	public int Order => 40;

	public bool Execute(PipelineContext ctx) {
		var rctx = (IResolveContext)ctx;
		if (rctx.Raw == null) {
			return true;
		}

		var groups = rctx.Raw
			.GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
			.ToList();

		var merged = new List<RawBeing>(groups.Count);
		foreach (var group in groups) {
			var result = group.First();
			foreach (var other in group.Skip(1)) {
				DeepMerge(result, other);
			}
			result.AssignedIndex = merged.Count;
			merged.Add(result);
		}

		rctx.Raw = merged;
		return true;
	}

	private static void DeepMerge(RawBeing target, RawBeing source) {
		foreach (var kv in source.Components) {
			target.Components[kv.Key] = kv.Value;
		}

		foreach (var kv in source.RawFields) {
			if (source.MergePolicyValue == MergePolicy.Replace) {
				target.RawFields[kv.Key] = kv.Value;
			}
			else if (!target.RawFields.TryGetValue(kv.Key, out var existing)) {
				target.RawFields[kv.Key] = kv.Value;
			}
			else if (existing is Dictionary<string, object?> tgtDict && kv.Value is Dictionary<string, object?> srcDict) {
				foreach (var sk in srcDict) {
					tgtDict[sk.Key] = sk.Value;
				}
			}
		}

		foreach (var f in source.FieldNames) {
			if (!target.FieldNames.Contains(f, StringComparer.OrdinalIgnoreCase)) {
				target.FieldNames.Add(f);
			}
		}

		foreach (var c in source.Concepts) {
			if (target.ConceptSet.Add(c)) {
				target.Concepts.Add(c);
			}
		}

		if (!string.IsNullOrEmpty(source.Inherits)) {
			target.Inherits = source.Inherits;
		}
	}
}
