namespace DataCatalyst.Core;

using System.Collections.Generic;
using Abstractions;

/// <summary>Plugin that hooks into the pipeline after the data graph is built.</summary>
public interface IGraphPlugin : IDataPlugin {
	/// <summary>Called after graph build, before catalog resolution.</summary>
	public void OnGraphBuilt(DataGraph graph, List<string> diagnostics);
}
