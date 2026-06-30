namespace Catalyst.Pipeline.Stages;

public sealed class ValidateStage : IPipelineStage {
	public int Order => 70;

	public bool Execute(PipelineContext ctx) {
		var rctx = (IResolveContext)ctx;
		if (rctx.Resolved == null) {
			return true;
		}

		var reg = rctx.Registries.Requires;
		foreach (var being in rctx.Resolved) {
			foreach (var conceptId in being.Concepts) {
				if (!rctx.Schema.TryGetConceptName(conceptId, out var cname) || cname == null) {
					continue;
				}

				var required = reg.GetRequired(cname);
				foreach (var req in required) {
					var aid = rctx.Schema.GetAspectId(req);
					if (aid.HasValue && !being.AspectFields.ContainsKey(aid.Value)) {
						rctx.Diagnostics.Error(
							$"Being '{being.Key}' for concept '{cname}' " +
							$"is missing required aspect '{req}'");
					}
				}

				var suggested = reg.GetSuggested(cname);
				foreach (var sug in suggested) {
					var aid = rctx.Schema.GetAspectId(sug);
					if (aid.HasValue && !being.AspectFields.ContainsKey(aid.Value)) {
						rctx.Diagnostics.Error(
							$"Being '{being.Key}' for concept '{cname}' " +
							$"is missing suggested aspect '{sug}'");
					}
				}
			}
		}

		return !rctx.Diagnostics.HasErrors;
	}
}
