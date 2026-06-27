namespace DataCatalyst.Knowledge;

public readonly ref struct ConceptScope<TConcept> where TConcept : struct, IConcept {
	internal readonly Knowledge Knowledge;

	internal ConceptScope(Knowledge knowledge) {
		Knowledge = knowledge;
	}

	public BeingScope<TConcept, TBeing> At<TBeing>()
		where TBeing : struct, IBeing, IBelongTo<TConcept>
		=> new(Knowledge.Pools[typeof(TConcept)], Knowledge.GetBeingIndex<TBeing>());

	public ConceptHandle<TConcept> At(int index)
		=> new(index);

	public T Take<T>(ConceptHandle<TConcept> handle) where T : struct
		=> Knowledge.Pools[typeof(TConcept)].Get<T>(handle.Index);
}
