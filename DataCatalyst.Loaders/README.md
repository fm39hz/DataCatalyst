# DataCatalyst.Loaders

JSON data loader for DataCatalyst. Reads JSON → typed `DataEntry` list.

```csharp
using DataCatalyst.Loaders;
var entries = JsonDataLoader.LoadDirectory("Data/");
```

## JSON format

```json
{
  "inherits": ["path.to.parent"],
  "ComponentName": { "field1": value1, "field2": value2 }
}
```

File path → key: `Entities/Hero.json` → `"Entities.Hero"`.

Uses `PrimitiveRegistry` to resolve component names to CLR types.
