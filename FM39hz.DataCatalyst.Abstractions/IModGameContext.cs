namespace FM39hz.DataCatalyst.Abstractions;

public interface IModGameContext {
	T? GetService<T>() where T : class;
	void RegisterService<T>(T service) where T : class;
}
