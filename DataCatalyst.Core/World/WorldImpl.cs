using System;
using System.Collections.Generic;
using DataCatalyst.Storage;
using WorldAbstractions = DataCatalyst.World;

namespace DataCatalyst.World;

internal static class WorldFactory
{
    public static WorldAbstractions.World Create(
        Dictionary<Type, IStoragePool> pools,
        Dictionary<Type, int> entryIndices)
    {
        return new WorldAbstractions.World(pools, entryIndices);
    }
}
