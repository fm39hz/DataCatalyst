using DataCatalyst.Registry;
using DataCatalyst.Storage;

namespace DataCatalyst.World;

public readonly ref struct ConceptScope<TConcept> where TConcept : struct, IConcept
{
    internal readonly IStoragePool Pool;

    internal ConceptScope(IStoragePool pool) => Pool = pool;

    public EntryScope<TConcept, TEntry> At<TEntry>()
        where TEntry : struct, IEntry, IBelongTo<TConcept>
        => new(Pool, EntryIndex<TEntry>.Value);
}
