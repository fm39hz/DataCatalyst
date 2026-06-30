namespace Catalyst.Knowledge;

using System;
using System.Collections.Generic;
using Catalyst.Storage;

internal sealed class DynamicAccess {
    private IReadOnlyDictionary<string, IStoragePool> _pools = new Dictionary<string, IStoragePool>();
    private IReadOnlyDictionary<string, int> _indices = new Dictionary<string, int>();

    internal void SetPools(Dictionary<string, IStoragePool> pools, Dictionary<string, int> indices) {
        _pools = pools;
        _indices = indices;
    }

    public IRawStoragePool? GetPool(string conceptName)
        => _pools.TryGetValue(conceptName, out var pool) ? pool as IRawStoragePool : null;

    public int GetIndex(string beingKey)
        => _indices.TryGetValue(beingKey, out var idx) ? idx : -1;

    public IEnumerable<string> GetConceptNames() => _pools.Keys;
}
