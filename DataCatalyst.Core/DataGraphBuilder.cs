namespace DataCatalyst.Core;

using System.Collections.Generic;

public static class DataGraphBuilder {
    public static DataGraph Build(IEnumerable<DataEntry> entries) {
        var graph = new DataGraph();
        foreach (var entry in entries)
            graph.Entries[entry.Key] = entry;
        return graph;
    }
}
