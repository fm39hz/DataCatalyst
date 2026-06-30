namespace Catalyst.SourceGen.Models;

using System;
using System.Collections.Generic;

public sealed record OntologyFile(string FileName, string Content);

public sealed class OntologyBuilder {
    public Dictionary<string, List<string>> Requires { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Suggests { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, string>> AspectFields { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddRequires(string concept, params string[] aspects) {
        if (!Requires.ContainsKey(concept)) Requires[concept] = [];
        Requires[concept].AddRange(aspects);
    }
    public void AddSuggests(string concept, params string[] aspects) {
        if (!Suggests.ContainsKey(concept)) Suggests[concept] = [];
        Suggests[concept].AddRange(aspects);
    }
    public void AddAspectFields(string aspect, Dictionary<string, string> fields) {
        AspectFields[aspect] = fields;
    }
}
