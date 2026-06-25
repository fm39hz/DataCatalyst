using DataCatalyst;
using DataCatalyst.World;
using DataCatalyst.Storage;

namespace Example;

public static class WorldHelper
{
    public static TAspect Get<TAspect, TConcept, TEntry>(this World world)
        where TConcept : struct, IConcept
        where TEntry : struct, IEntry, IBelongTo<TConcept>
        where TAspect : struct
    {
        if (world.Pools.TryGetValue(typeof(TConcept), out var pool))
        {
            var index = world.GetEntryIndex(typeof(TEntry));
            return pool.Get<TAspect>(index);
        }
        return default;
    }
}
