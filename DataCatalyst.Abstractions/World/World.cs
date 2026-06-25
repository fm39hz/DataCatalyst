using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using DataCatalyst.Schema;
using DataCatalyst.Storage;

namespace DataCatalyst.World;

public sealed class World
{
    internal readonly FrozenDictionary<Type, IStoragePool> Pools;
    internal readonly FrozenDictionary<Type, int> EntryIndices;
    internal readonly SchemaRegistry? Schema;

    internal World(Dictionary<Type, IStoragePool> pools, Dictionary<Type, int> entryIndices,
        SchemaRegistry? schema = null)
    {
        Pools = pools.ToFrozenDictionary();
        EntryIndices = entryIndices.ToFrozenDictionary();
        Schema = schema;
    }

    public ConceptScope<TConcept> FromConcept<TConcept>()
        where TConcept : struct, IConcept
    {
        if (!Pools.TryGetValue(typeof(TConcept), out var pool))
            throw new InvalidOperationException(
                $"Concept '{typeof(TConcept).Name}' has no data in this world");
        return new ConceptScope<TConcept>(this);
    }

    public int GetEntryIndex(Type entryType)
        => EntryIndices.TryGetValue(entryType, out var idx) ? idx : -1;
}
