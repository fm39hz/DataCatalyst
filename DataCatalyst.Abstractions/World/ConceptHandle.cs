namespace DataCatalyst.World;

public readonly struct ConceptHandle<TConcept> where TConcept : struct, IConcept
{
    public int Index { get; }

    public ConceptHandle(int index)
    {
        Index = index;
    }
}
