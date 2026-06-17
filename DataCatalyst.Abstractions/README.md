# DataCatalyst.Abstractions

Zero-dependency abstractions and contracts for the DataCatalyst composition framework.

## 📦 Core Types

| Type                            | Description                                                                                                                                         |
|---------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `[DataComponent]`               | Applied to structs to mark them as discoverable composition data components.                                                                        |
| `[DataPlugin]`                  | Applied to classes to register them as custom plugins with optional dependency ordering (`DependsOn`).                                              |
| `IDataPlugin`                   | Marker interface for plugins.                                                                                                                       |
| `DataKey<T>`                    | A type-safe, read-only cross-reference pointing to another entry by string ID.                                                                      |
| `IDataViewAdapter<T>`           | Engine bridge interface providing hook callbacks for entry lifecycle changes (`OnEntryAdded`, `OnEntryRemoved`, `OnEntryModified`, `OnAllCleared`). |
| `IDataRepository<TKey, TValue>` | Generic repository contract for data management.                                                                                                    |
| `IFormatReader<TValue>`         | Parser contract for custom file loading.                                                                                                            |
