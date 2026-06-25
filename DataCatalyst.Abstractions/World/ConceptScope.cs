namespace DataCatalyst.World;

public readonly ref struct ConceptScope<TConcept> where TConcept : struct, IConcept {
	internal readonly World World;

	internal ConceptScope(World world) {
		World = world;
	}

	public EntryScope<TConcept, TEntry> At<TEntry>()
		where TEntry : struct, IEntry, IBelongTo<TConcept>
		=> new(World.Pools[typeof(TConcept)], World.GetEntryIndex(typeof(TEntry)));

	public ConceptHandle<TConcept> At(int index)
		=> new(index);

	public T Take<T>(ConceptHandle<TConcept> handle) where T : struct
		=> World.Pools[typeof(TConcept)].Get<T>(handle.Index);
}
