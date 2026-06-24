using System;
using System.Collections.Generic;
using WorldAbstractions = DataCatalyst.World;

namespace DataCatalyst.World;

internal static class WorldFactory
{
    public static WorldAbstractions.World Create(Dictionary<Type, DataCatalyst.Storage.IStoragePool> pools)
    {
        return new WorldAbstractions.World(pools);
    }
}
