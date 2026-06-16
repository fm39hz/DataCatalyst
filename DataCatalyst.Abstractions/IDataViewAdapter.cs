namespace DataCatalyst.Abstractions;

public interface IDataViewAdapter<T> {
	void OnEntryAdded(string key, T entry);
	void OnEntryRemoved(string key);
	void OnEntryModified(string key, T oldEntry, T newEntry);
	void OnAllCleared();
}
