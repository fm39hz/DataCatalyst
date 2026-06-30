namespace Catalyst.SourceGen.Models;

using System.Collections.Generic;

public sealed record FrameworkData(
    Dictionary<string, string> ConceptNsMap,
    HashSet<string> ConceptNames,
    HashSet<string> AspectNames,
    List<AspectInfo?> Aspects);
