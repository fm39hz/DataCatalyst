namespace DataCatalyst.Core;

using System.Collections.Generic;

public sealed class DataGraph {
    public Dictionary<string, DataEntry> Entries { get; } = new();

    public DataGraph() { }

    public DataGraph(Dictionary<string, DataEntry> entries) {
        Entries = entries;
    }
}
