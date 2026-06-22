namespace DataCatalyst.Core;

/// <summary>Aggregates all registries for a DataCatalyst scope. Enables independent environments for testing, multi-catalog, and hot reload.</summary>
public sealed class DataCatalystEnvironment {
	/// <summary>Default environment for backward compatibility.</summary>
	public static readonly DataCatalystEnvironment Default = new();

	/// <summary>Registered plugins.</summary>
	public PluginRegistry Plugins { get; }

	/// <summary>Registered component types and their JSON discriminators.</summary>
	public PrimitiveRegistry Primitives { get; }

	/// <summary>Registered singleton services.</summary>
	public ServiceRegistry Services { get; }

	/// <summary>Registered mappers for bake-time consumption.</summary>
	public MapperRegistry Mappers { get; }

	/// <summary>Schema registry for field name→type mapping.</summary>
	public SchemaBuilder Schema { get; }

	/// <summary>Registered data view adapters.</summary>
	public DataViewAdapterRegistry ViewAdapters { get; }

	public DataCatalystEnvironment() {
		Plugins = new PluginRegistry();
		Primitives = new PrimitiveRegistry();
		Services = new ServiceRegistry();
		Mappers = new MapperRegistry();
		ViewAdapters = new DataViewAdapterRegistry();
		Schema = new SchemaBuilder();
	}
}
