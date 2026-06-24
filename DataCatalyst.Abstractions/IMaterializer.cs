namespace DataCatalyst;

public interface IMaterializer<TTarget>
{
    void Apply<T>(TTarget target, T component) where T : struct;
}
