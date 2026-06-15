# DataCatalyst Modding Plugin - Examples

These examples demonstrate the **4 rings** of the modding ladder:

| Ring | Example | Shows |
|---|---|---|
| **1 - Data** | `HelloMod/` | JSON override + manifest + mod lifecycle |
| **2 - Script opt-in** | `WeaverHook/` | `[ModHook]` attribute + Hook.Before/After |
| **3 - Script auto** | `WeaverHook/` (config) | `EnableModHookWeaver=true` |
| **4 - C# merge** | `FrifloPlugin/` (in parent) | `[ModPlugin]` + `EnableModMerge` |

## Layout

```
modding_plugins/
├── README.md
├── HelloMod/                    ← Ring 1
│   ├── mod.json
│   ├── items_override.json
│   └── components.json
│
└── WeaverHook/                  ← Ring 2-3
    ├── Game.cs                   ← game code với [ModHook]
    ├── mod.json
    ├── init.lua                  ← Hook.Before/After
    └── .csproj                   ← EnableModHookWeaver config
```

## Game setup

```xml
<ItemGroup>
  <PackageReference Include="FM39hz.DataCatalyst" />
  <PackageReference Include="FM39hz.DataCatalyst.Plugins.Modding" />
</ItemGroup>

<!-- Ring 2: only [ModHook] methods get dispatch -->
<!-- Ring 3: uncomment for auto-weave all methods -->
<PropertyGroup>
  <EnableModHookWeaver>false</EnableModHookWeaver>
</PropertyGroup>
```
