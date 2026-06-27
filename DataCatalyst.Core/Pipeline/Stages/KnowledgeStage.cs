namespace DataCatalyst.Pipeline.Stages;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Knowledge;
using DataCatalyst.Registry;
using DataCatalyst.Storage;

public sealed class KnowledgeStage : IPipelineStage {
	public int Order => 90;

	public bool Execute(PipelineContext ctx) {
		var rctx = (IResolveContext)ctx;
		var bctx = (IBakeContext)ctx;
		if (rctx.Resolved == null || rctx.Resolved.Count == 0) return true;

		var br = rctx.Registries.Beings;
		var atr = rctx.Registries.AspectTypes;
		var byConcept = BuildConceptGroups(rctx.Resolved);

		var pools = new Dictionary<Type, ITypedStoragePool>();
		var beingIdx = new Dictionary<Type, int>();
		var dynPools = new Dictionary<string, IStoragePool>(StringComparer.OrdinalIgnoreCase);
		var dynIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		var beingByTypeName = rctx.Resolved
			.Where(e => !string.IsNullOrEmpty(e.Key))
			.ToDictionary(e => e.Key, e => e, StringComparer.OrdinalIgnoreCase);

		foreach (var kv in byConcept) {
			var ct = FindType(kv.Key, rctx.Schema, br);
			var ce = kv.Value;
			var max = ce.Max(e => e.AssignedIndex);
			if (max < 0) continue;

			var cn = rctx.Schema.TryGetConceptName(kv.Key, out var n) ? n : null;
			var allowed = rctx.Schema.GetConceptAspects(kv.Key);

			if (ct != null) {
				var pool = CreateTypedPool(ce, max, allowed, ct, br, rctx, atr, beingByTypeName, beingIdx);
				pools[ct] = pool;
			}
			else if (cn != null) {
				var dp = CreateDynamicPool(ce, max, dynIdx);
				dynPools[cn] = dp;
			}
		}

		var k = new Knowledge(pools, beingIdx, rctx.Schema, []);
		k.SetDynamicPools(dynPools, dynIdx);
		bctx.Knowledge = k;
		return true;
	}

	private static Dictionary<int, List<ResolvedBeing>> BuildConceptGroups(List<ResolvedBeing> resolved) {
		var byConcept = new Dictionary<int, List<ResolvedBeing>>();
		foreach (var e in resolved) {
			foreach (var c in e.Concepts) {
				if (!byConcept.TryGetValue(c, out var l)) {
					byConcept[c] = l = [];
				}
				l.Add(e);
			}
		}
		return byConcept;
	}

	private static ITypedStoragePool CreateTypedPool(
		List<ResolvedBeing> ce, int max, List<int>? allowed,
		Type ct, IBeingRegistry br, IResolveContext rctx, IAspectTypeRegistry atr,
		Dictionary<string, ResolvedBeing> beingByTypeName,
		Dictionary<Type, int> beingIdx)
	{
		var hasDyn = allowed != null && allowed.Any(a =>
			rctx.Schema.TryGetAspectName(a, out var _an) && _an != null && !atr.HasType(_an));
		var pool = hasDyn ? new DynamicPool() : (br.CreatePool(ct) ?? new GenericPool());
		pool.Resize(max + 1);
		foreach (var e in ce) {
			foreach (var comp in e.Components) {
				if (allowed == null || rctx.Schema.GetAspectId(comp.Key.Name) is int aid && allowed.Contains(aid)) {
					if (pool is IRawStoragePool rawPool) {
						rawPool.SetRaw(e.AssignedIndex, comp.Key, comp.Value);
					}
				}
			}
		}

		foreach (var rec in br.All) {
			if (!rec.Concepts.Contains(ct)) continue;
			if (beingByTypeName.TryGetValue(rec.BeingType.Name, out var fb)) {
				beingIdx[rec.BeingType] = fb.AssignedIndex;
			}
		}

		return pool;
	}

	private static IStoragePool CreateDynamicPool(
		List<ResolvedBeing> ce, int max,
		Dictionary<string, int> dynIdx)
	{
		var dp = new DynamicPool();
		dp.Resize(max + 1);
		foreach (var e in ce) {
			foreach (var kv2 in e.AspectFields) {
				dp.SetRawValue(e.AssignedIndex, kv2.Key, kv2.Value);
			}
			if (!string.IsNullOrEmpty(e.Key) && !dynIdx.ContainsKey(e.Key)) {
				dynIdx[e.Key] = e.AssignedIndex;
			}
		}
		return dp;
	}

	private static Type? FindType(int id, Schema.SchemaRegistry sc, IBeingRegistry br) {
		if (!sc.TryGetConceptName(id, out var n) || n == null) return null;
		foreach (var r in br.All) {
			foreach (var c in r.Concepts) {
				if (string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase)) {
					return c;
				}
			}
		}
		return null;
	}
}
