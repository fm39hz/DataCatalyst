namespace DataCatalyst.World;

using System;
using System.Collections.Generic;
using DataCatalyst.Schema;
using DataCatalyst.Storage;
using WorldAbstractions = DataCatalyst.World;

internal static class WorldFactory {
	public static World Create(
		Dictionary<Type, IStoragePool> pools,
		Dictionary<Type, int> entryIndices,
		SchemaRegistry? schema = null) => new World(pools, entryIndices, schema);
}
