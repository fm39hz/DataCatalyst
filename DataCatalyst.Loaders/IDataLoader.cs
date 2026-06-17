namespace DataCatalyst.Loaders;

using System.Collections.Generic;
using DataCatalyst.Core;

/// <summary>Loads data entries from an external source.</summary>
public interface IDataLoader {
	/// <summary>Loads entries from the given source path.</summary>
	List<DataEntry> LoadDirectory(string path);
}
