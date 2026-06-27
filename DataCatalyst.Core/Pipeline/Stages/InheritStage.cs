namespace DataCatalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class InheritStage : IPipelineStage {
	public int Order => 50;

	public bool Execute(PipelineContext ctx) {
		var rctx = (IResolveContext)ctx;
		if (rctx.Raw == null) {
			return true;
		}

		var lookup = rctx.Raw.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

		foreach (var being in rctx.Raw) {
			if (string.IsNullOrEmpty(being.Inherits)) {
				continue;
			}

			var ancestors = new List<string>();
			var current = being.Inherits;

			while (current != null && lookup.TryGetValue(current, out var parent)) {
				if (ancestors.Contains(parent.Key)) {
					rctx.Diagnostics.Error(
						$"Circular inheritance detected: '{being.Key}' → '... → '{parent.Key}' → '{parent.Inherits}'");
					ancestors.Clear();
					break;
				}
				ancestors.Add(parent.Key);
				current = string.IsNullOrEmpty(parent.Inherits) ? null : parent.Inherits;
			}

			ancestors.Reverse();

			foreach (var ancestorKey in ancestors) {
				if (!lookup.TryGetValue(ancestorKey, out var ancestor)) {
					continue;
				}

				foreach (var f in ancestor.FieldNames) {
					if (!being.FieldNames.Contains(f, StringComparer.OrdinalIgnoreCase)) {
						being.FieldNames.Add(f);
					}
				}

				foreach (var kv in ancestor.RawFields) {
					being.RawFields.TryAdd(kv.Key, kv.Value);
				}

				foreach (var c in ancestor.Concepts) {
					if (being.ConceptSet.Add(c)) {
						being.Concepts.Add(c);
					}
				}

				foreach (var kv in ancestor.Components) {
					being.Components.TryAdd(kv.Key, kv.Value);
				}
			}
		}

		return !rctx.Diagnostics.HasErrors;
	}
}
