# DataCatalyst.Loaders

An AOT-safe JSON data loader for the DataCatalyst framework.

## 🚀 Usage

Load a directory containing JSON files into a list of raw `DataEntry` containers:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Loaders;

// Provide JsonSerializerOptions for Native AOT compatibility
var options = new JsonSerializerOptions
{
    TypeInfoResolver = new DefaultJsonTypeInfoResolver()
};

var loadResult = JsonDataLoader.LoadDirectory("Data/", options);
```

## 📄 JSON Format

JSON files define an inherits list and component objects:

`Entities/Goblin.json`:
```json
{
  "inherits": [ "Entities.BaseMonster" ],
  "Health": {
    "Current": 50,
    "Max": 50
  }
}
```

* **Entry Keys**: Generated automatically based on relative file paths (e.g., `Entities/Goblin.json` relative to `Data/` becomes `"Entities.Goblin"`).
* **Component Resolution**: The loader reads properties (like `"Health"`) and resolves them against types registered in `PrimitiveRegistry`. If a component name is ambiguous (declared in multiple namespaces), you must use its fully-qualified type name (e.g., `"MyGame.Components.Health"`).
