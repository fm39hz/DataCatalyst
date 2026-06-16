namespace DataCatalyst.Abstractions;

public readonly struct DataRef<TTarget, TTargetKind> where TTargetKind : struct {
	public TTargetKind Kind { get; }
	public DataRef(TTargetKind kind) => Kind = kind;
}
