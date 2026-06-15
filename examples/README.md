# DataCatalyst - Examples

## Modding plugin examples

| Example | Ring | Shows |
|---|---|---|
| [`modding_plugins/HelloMod/`](modding_plugins/HelloMod/) | 1 - Data | JSON override + manifest + `ModLoader.Scan` |
| [`modding_plugins/WeaverHook/`](modding_plugins/WeaverHook/) | 2-3 - Script | `[ModHook]` + `Hook.Before`/`After` + auto-weave |

## Engine adapter examples

| Example | Shows |
|---|---|
| [`FrifloPlugin/`](FrifloPlugin/) | Bridge data → Friflo Entity + components via `IDataViewAdapter` |
| [`GodotPlugin/`](GodotPlugin/) | Bridge data → ItemNode (C# subclass) via `IDataViewAdapter` |
| [`ScriptingBridge/`](ScriptingBridge/) | Lua VM exposing DataCatalyst + ECS API (legacy approach) |
