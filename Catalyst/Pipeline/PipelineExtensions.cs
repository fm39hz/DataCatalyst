namespace Catalyst.Pipeline;

using System;
using System.IO;
using Catalyst.Loader;

public static class PipelineExtensions {
	public static Pipeline AddSource(this Pipeline p, string name, IDataLoader loader, string path, Action<DataSource>? configure = null) {
		var s = new DataSource(name, loader, path);
		configure?.Invoke(s);

		if (Directory.Exists(path)) {
			foreach (var file in Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories)) {
				var content = File.ReadAllText(file);
				switch (loader.DetectFileType(content)) {
					case LoaderFileType.Concept:
						s.ConceptFiles.Add(file);
						break;
					case LoaderFileType.Aspect:
						s.AspectFiles.Add(file);
						break;
				}
			}
		}

		p._sources.Add(s);
		return p;
	}

	public static Pipeline AddBaker(this Pipeline p, IBaker baker) { p._bakers.Add(baker); return p; }
	public static Pipeline AddStage(this Pipeline p, IPipelineStage stage) { p._stages.Add(stage); return p; }
}
