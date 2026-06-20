namespace DataCatalyst.Abstractions;

/// <summary>Plugin lifecycle: called after pipeline initialization.</summary>
public interface IPluginInit {
	/// <summary>Called after all plugins are loaded and the pipeline is ready.</summary>
	void OnPluginInit();
}

/// <summary>Plugin lifecycle: called during pipeline teardown.</summary>
public interface IPluginCleanup : IPluginInit {
	/// <summary>Called before pipeline shutdown. Plugins should release resources.</summary>
	void OnPluginCleanup();
}
