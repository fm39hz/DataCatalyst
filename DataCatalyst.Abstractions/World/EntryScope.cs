using DataCatalyst.Storage;

namespace DataCatalyst.World;

public readonly ref struct EntryScope<TConcept, TEntry>
    where TConcept : struct, IConcept
    where TEntry : struct, IEntry, IBelongTo<TConcept>
{
    internal readonly IStoragePool Pool;
    internal readonly int Index;

    internal EntryScope(IStoragePool pool, int index)
    {
        Pool = pool;
        Index = index;
    }

    public ref readonly T Take<T>() where T : struct
        => ref Pool.Get<T>(Index);
}
