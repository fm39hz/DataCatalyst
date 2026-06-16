namespace FM39hz.DataCatalyst.Runtime;

using System.Collections.Generic;
using FM39hz.DataCatalyst.Abstractions;

public static class DslReaderRegistry {
    private static readonly Dictionary<(string extension, System.Type type), object> _readers = new();
    private static readonly object _lock = new();

    public static void Register<TValue>(IDslReader<TValue> reader) {
        lock (_lock) {
            var key = (reader.FileExtension, typeof(TValue));
            _readers[key] = reader;
        }
    }

    public static IEnumerable<IDslReader<TValue>> GetReaders<TValue>() {
        List<IDslReader<TValue>> snap;
        lock (_lock) {
            snap = new List<IDslReader<TValue>>(_readers.Count);
            var target = typeof(TValue);
            foreach (var kvp in _readers) {
                if (kvp.Key.type == target)
                    snap.Add((IDslReader<TValue>)kvp.Value);
            }
        }
        return snap;
    }
}
