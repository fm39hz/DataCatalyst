# HelloMod — Data override + script mod

Demonstrates: JSON override + manifest + `ModLoader.Scan` + `ModLoader.LoadAllSources`

## Manifest

```json
{
  "id": "HelloMod",
  "version": "1.0.0",
  "gameVersion": ">=1.0.0",
  "content": [
    { "type": "data", "file": "items_override.json", "target": "Item" },
    { "type": "script", "file": "init.lua" }
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

## Lua script

```lua
-- init.lua
Item_Add("CustomSword", { Health = 10, Weight = 2.0 })
local item = Item_Get("Potion")
Log("Potion health: " .. item.Health)
```

## Game integration

```csharp
[CatalystData("Items.json")]
public partial struct Item { }

ModLoader.LoadAllSources(new[] {
    ModSource.From("Data/"),
    ModSource.From("Mods/"),
}, new LuaScriptEngine(), new ComponentSchemaRegistry());
```
