using DataCatalyst;
using DataCatalyst.World;
using DataCatalyst.Registry;
using DataCatalyst.Stages;

namespace Example;

public static class WorldHelper
{
    public static TAspect Get<TAspect, TConcept, TEntry>(this World world)
        where TConcept : struct, IConcept
        where TEntry : struct, IEntry, IBelongTo<TConcept>
        where TAspect : struct
    {
        var pool = (GenericPool)world.Pools[typeof(TConcept)];
        return pool.Get<TAspect>(EntryIndex<TEntry>.Value);
    }
}
