namespace DataCatalyst.World;

public readonly struct ConceptHandle<TConcept>(int index) where TConcept : struct, IConcept {
	public int Index { get; } = index;
}
