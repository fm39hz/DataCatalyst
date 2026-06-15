# HelloMod - Ring 1: Data-only modding

Demonstrates: JSON override + manifest + `ModLoader.Scan` + `ModLoader.Load`

## Manifest

```json
{
  "id": "HelloMod",
  "version": "1.0.0",
  "gameVersion": ">=1.0.0",
  "content": [
    { "type": "data", "file": "items_override.json", "target": "Item" }
  ]
}
```

## Data override

```json
{
  "SuperPotion": { "Health": 999, "Weight": 0.5 },
  "UltraSword":  { "Health": 0,   "Weight": 5.0 }
}
```

## Game integration

```csharp
[CatalystData("Items.json")]
public partial struct Item { }

var results = ModLoader.Scan("Mods/");
foreach (var r in results) {
    if (!r.Success) Log.Error($"{r.ModId}: {r.Error}");
}

// Data overrides loaded via existing ItemMod.LoadMods()
ItemMod.LoadMods("Mods/HelloMod/");

// Or via ModLoader with IScriptEngine:
// ModLoader.Load("Mods/", new LuaScriptEngine(), new ComponentSchemaRegistry());
```
