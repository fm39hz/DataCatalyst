namespace DataCatalyst.Abstractions;

/// <summary>Observer for data entry lifecycle events.</summary>
public interface IDataViewAdapter<T> {
	/// <summary>Called when an entry is added.</summary>
	public void OnEntryAdded(string key, T entry);
	/// <summary>Called when an entry is removed.</summary>
	public void OnEntryRemoved(string key);
	/// <summary>Called when an entry is modified.</summary>
	public void OnEntryModified(string key, T oldEntry, T newEntry);
	/// <summary>Called when all entries are cleared.</summary>
	public void OnAllCleared();
}
