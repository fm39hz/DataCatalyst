using System;
using System.Collections.Generic;
using DataCatalyst.Storage;

namespace DataCatalyst.World;

public sealed class World
{
    internal readonly Dictionary<Type, IStoragePool> Pools;

    internal World(Dictionary<Type, IStoragePool> pools)
    {
        Pools = pools;
    }

    public ConceptScope<TConcept> FromConcept<TConcept>()
        where TConcept : struct, IConcept
    {
        if (!Pools.TryGetValue(typeof(TConcept), out var pool))
            throw new InvalidOperationException(
                $"Concept '{typeof(TConcept).Name}' has no data in this world");
        return new ConceptScope<TConcept>(pool);
    }
}
