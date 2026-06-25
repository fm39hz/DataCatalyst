namespace DataCatalyst.World;

using System;
using System.Collections.Generic;
using DataCatalyst.Schema;
using DataCatalyst.Storage;

internal static class WorldFactory {
	public static World Create(
		Dictionary<Type, IStoragePool> pools,
		Dictionary<Type, int> entryIndices,
		SchemaRegistry? schema = null) => new(pools, entryIndices, schema);
}
