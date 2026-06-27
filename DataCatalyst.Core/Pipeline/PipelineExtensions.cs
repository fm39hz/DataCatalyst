namespace DataCatalyst.Pipeline;

using System;
using System.IO;
using DataCatalyst.Loader;

public static class PipelineExtensions {
	public static Pipeline AddSource(this Pipeline p, string name, IDataLoader loader, string path, Action<DataSource>? configure = null) {
		var s = new DataSource(name, loader, path);
		configure?.Invoke(s);
		p._sources.Add(s);
		return p;
	}
	public static Pipeline AddOntology(this Pipeline p, string path, string[] patterns) {
		if (!Directory.Exists(path)) {
			return p;
		}

		foreach (var pat in patterns) {
			if (pat.StartsWith("*/")) {
				var file = pat[2..];
				foreach (var sub in Directory.EnumerateDirectories(path)) {
					if (File.Exists(Path.Combine(sub, file))) {
						p._ontology.Add(Path.Combine(sub, file));
					}
				}
			}
			else if (File.Exists(Path.Combine(path, pat))) {
				p._ontology.Add(Path.Combine(path, pat));
			}
		}
		return p;
	}
	public static Pipeline AddOntology(this Pipeline p, string path) {
		if (File.Exists(path)) {
			p._ontology.Add(path);
		}

		return p;
	}
	public static Pipeline AddBaker(this Pipeline p, IBaker baker) { p._bakers.Add(baker); return p; }
	public static Pipeline AddStage(this Pipeline p, IPipelineStage stage) { p._stages.Add(stage); return p; }
}
