namespace DataCatalyst.Core;

using System.Collections.Generic;
using Abstractions;

/// <summary>Plugin that hooks into the pipeline after raw entries are loaded.</summary>
public interface IPostLoadPlugin : IPlugin {
	/// <summary>Called after loading entries, before graph build. Plugins can filter or augment entries.</summary>
	public void OnEntriesLoaded(IReadOnlyList<DataEntry> entries, List<string> diagnostics);
}
