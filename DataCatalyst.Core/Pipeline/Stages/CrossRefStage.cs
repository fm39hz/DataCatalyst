namespace DataCatalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CrossRefStage : IPipelineStage {
	public int Order => 80;
	public bool Execute(PipelineContext ctx) {
		var rctx = (IResolveContext)ctx;
		var lctx = (ILoadContext)ctx;
		if (rctx.Resolved == null || rctx.Schema == null) {
			return true;
		}

		const int maxDepth = 64;
		var atr = rctx.Registries.AspectTypes;
		foreach (var e in rctx.Resolved) {
			foreach (var kv in e.AspectFields) {
				var (resolved, _) = ResolveRefs(kv.Value, lctx.Keys, maxDepth);
				e.AspectFields[kv.Key] = resolved;
			}
			foreach (var kv in e.AspectFields) {
				var aspectName = rctx.Schema.TryGetAspectName(kv.Key, out var n) ? n : null;
				if (aspectName == null) continue;

				if (atr.TryGetType(aspectName, out var type) && type != null) {
					try {
						var deserialized = atr.Deserialize(type, kv.Value);
						if (deserialized != null) {
							e.Components[type] = deserialized;
						}
					}
					catch (Exception ex) { rctx.Diagnostics.Error($"Deserialize '{aspectName}': {ex.Message}"); }
				}
			}
		}
		return true;
	}

	private static (object? Result, bool Modified) ResolveRefs(object? node, HashSet<string> knownKeys, int maxDepth, int depth = 0) {
		if (node == null || depth > maxDepth) {
			return (node, false);
		}

		if (node is Dictionary<string, object?> dict) {
			if (dict.TryGetValue("$ref", out var refValue) && refValue is string targetKey) {
				return (targetKey, true);
			}
			var modified = false;
			foreach (var key in dict.Keys.ToList()) {
				var (resolved, childModified) = ResolveRefs(dict[key], knownKeys, maxDepth, depth + 1);
				if (childModified) {
					dict[key] = resolved;
					modified = true;
				}
			}
			return (dict, modified);
		}

		if (node is List<object?> list) {
			var modified = false;
			for (var i = 0; i < list.Count; i++) {
				var (resolved, childModified) = ResolveRefs(list[i], knownKeys, maxDepth, depth + 1);
				if (childModified) {
					list[i] = resolved;
					modified = true;
				}
			}
			return (list, modified);
		}

		return (node, false);
	}
}
