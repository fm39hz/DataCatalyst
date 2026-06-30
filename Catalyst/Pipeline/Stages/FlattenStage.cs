namespace Catalyst.Pipeline.Stages;

using Catalyst.Knowledge;
using Catalyst.Storage;

public sealed class FlattenStage : IPipelineStage {
	public int Order => 110;

	public bool Execute(PipelineContext ctx) {
		var bctx = (IBakeContext)ctx;
		if (bctx.Knowledge == null) return true;

		var knowledge = bctx.Knowledge;
		var store = new FlatStore();

		// Gọi Flatten trên mỗi typed pool
		foreach (var pool in knowledge.Pools.Values) {
			if (pool is IFlatPool flat) {
				flat.Flatten(store);
			}
		}

		if (store._flats.Count > 0) {
			knowledge._flatStore = store;
		}
		return true;
	}
}
