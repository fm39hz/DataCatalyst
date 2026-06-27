namespace DataCatalyst.Knowledge;

using System;
using System.Collections.Generic;

internal sealed class BakedCache {
    private readonly IReadOnlyDictionary<Type, IReadOnlyDictionary<string, object>> _cache;

    internal BakedCache(Dictionary<Type, Dictionary<string, object>> source) {
        var cache = new Dictionary<Type, IReadOnlyDictionary<string, object>>();
        foreach (var kv in source) cache[kv.Key] = kv.Value;
        _cache = cache;
    }

    public TBaked Get<TBaked>(string beingKey) {
        if (_cache.TryGetValue(typeof(TBaked), out var inner) && inner.TryGetValue(beingKey, out var obj))
            return (TBaked)obj;
        throw new KeyNotFoundException($"No baked data for '{typeof(TBaked).Name}' / '{beingKey}'");
    }

    public IReadOnlyDictionary<string, TBaked> GetAll<TBaked>() {
        if (_cache.TryGetValue(typeof(TBaked), out var inner)) {
            var result = new Dictionary<string, TBaked>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in inner) result[kv.Key] = (TBaked)kv.Value;
            return result;
        }
        return new Dictionary<string, TBaked>();
    }

    public bool TryGet<TBaked>(string beingKey, out TBaked result) {
        if (_cache.TryGetValue(typeof(TBaked), out var inner) && inner.TryGetValue(beingKey, out var obj)) {
            result = (TBaked)obj;
            return true;
        }
        result = default!;
        return false;
    }

    public bool HasType(Type type) => _cache.ContainsKey(type);
}
