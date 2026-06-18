namespace DataCatalyst.Loaders;

using Core;

/// <summary>Loads data entries from an external source.</summary>
public interface IDataLoader {
	/// <summary>Loads entries from the given source path.</summary>
	public LoadResult LoadDirectory(string path);
}
