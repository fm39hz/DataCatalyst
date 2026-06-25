namespace DataCatalyst;

public interface IMaterializer<TTarget> {
	public void Apply<T>(TTarget target, T component) where T : struct;
}
