namespace DataCatalyst.Knowledge;

using DataCatalyst.Storage;

public readonly ref struct BeingScope<TConcept, TBeing>
	where TConcept : struct, IConcept
	where TBeing : struct, IBeing, IBelongTo<TConcept> {
	internal readonly IStoragePool Pool;
	internal readonly int Index;

	internal BeingScope(IStoragePool pool, int index) {
		Pool = pool;
		Index = index;
	}

	public ref readonly T Take<T>() where T : struct
		=> ref Pool.Get<T>(Index);
}
