using System;
using System.Collections.Generic;

namespace DataCatalyst.Ontology;

public readonly struct OntologyFile {
	public string FileName { get; }
	public string Content { get; }

	public OntologyFile(string fileName, string content) {
		FileName = fileName;
		Content = content;
	}
}

public sealed class OntologyBuilder {
	public Dictionary<string, List<string>> Requires { get; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, List<string>> Suggests { get; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, Dictionary<string, string>> AspectFields { get; } = new(StringComparer.OrdinalIgnoreCase);

	public void AddRequires(string concept, params string[] aspects) {
		if (!Requires.ContainsKey(concept))
			Requires[concept] = [.. aspects];
	}

	public void AddSuggests(string concept, params string[] aspects) {
		if (!Suggests.ContainsKey(concept))
			Suggests[concept] = [.. aspects];
	}

	public void AddAspectFields(string aspect, Dictionary<string, string> fields) {
		if (!AspectFields.ContainsKey(aspect))
			AspectFields[aspect] = fields;
	}
}

public interface IOntologyParser {
	bool CanHandle(in OntologyFile file);
	void Parse(in OntologyFile file, OntologyBuilder builder);
}
