using System;
using System.Collections.Generic;
using DataCatalyst.Storage;

namespace DataCatalyst.World;

public sealed class World
{
    internal readonly Dictionary<Type, IStoragePool> Pools;
    internal readonly Dictionary<Type, int> EntryIndices;

    internal World(Dictionary<Type, IStoragePool> pools, Dictionary<Type, int> entryIndices)
    {
        Pools = pools;
        EntryIndices = entryIndices;
    }

    public ConceptScope<TConcept> FromConcept<TConcept>()
        where TConcept : struct, IConcept
    {
        if (!Pools.TryGetValue(typeof(TConcept), out var pool))
            throw new InvalidOperationException(
                $"Concept '{typeof(TConcept).Name}' has no data in this world");
        return new ConceptScope<TConcept>(this);
    }

    /// <summary>Looks up the assigned storage index for an entry type.</summary>
    public int GetEntryIndex(Type entryType)
        => EntryIndices.TryGetValue(entryType, out var idx) ? idx : -1;
}
