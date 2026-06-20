namespace DataCatalyst.Abstractions;

/// <summary>Base interface for all plugins. Plugins hook the pipeline and receive diagnostics.</summary>
public interface IPlugin {
	/// <summary>Whether this plugin is enabled. Disabled plugins are skipped in the pipeline.</summary>
	bool IsEnabled { get; }

	/// <summary>Called after all plugins are registered. Plugins can resolve dependencies from ServiceRegistry here.</summary>
	void OnLoad();
}
