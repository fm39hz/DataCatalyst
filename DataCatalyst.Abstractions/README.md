# DataCatalyst.Abstractions

Zero-dependency contracts for DataCatalyst. netstandard2.1.

| Type | Role |
|------|------|
| `[DataComponent]` | Mark any type as composable data component |
| `[DataPlugin]` | Mark plugin class with optional `DependsOn` |
| `DataKey<T>` | Typed cross-reference by string key |
| `IDataPlugin` | Plugin marker interface |
| `IDataViewAdapter<T>` | Engine bridge: `OnEntryAdded/Removed/Modified/AllCleared` |
| `IDataRepository<TKey, TValue>` | Generic repository: `Get/TryGet/GetAll/Count` |
| `IFormatReader<TValue>` | Optional format parser: `FileExtension` + `TryRead` |
