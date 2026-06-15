namespace FM39hz.DataCatalyst.Runtime;

using FM39hz.DataCatalyst.Abstractions;

public sealed class ModGameContext : IModGameContext {
	public T? GetService<T>() where T : class => ServiceRegistry.Get<T>();
	public void RegisterService<T>(T service) where T : class => ServiceRegistry.Register(service);
}
