namespace DataCatalyst.Knowledge;

using System;
using System.Collections.Generic;
using DataCatalyst.Schema;
using DataCatalyst.Storage;

internal static class KnowledgeFactory {
	public static Knowledge Create(
		Dictionary<Type, IStoragePool> pools,
		Dictionary<Type, int> beingIndices,
		SchemaRegistry? schema = null) => new(pools, beingIndices, schema);
}
