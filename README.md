# UniversalDataDriven

A Roslyn source generator that bakes JSON data into strongly-typed static C# catalogs. Designed for performance-critical paths where reflection and runtime parsing are unacceptable.

## Why this?

This is an opinionated data-loading solution originally built for my own games. It solves the "Data-Driven vs. Performance" trade-off by moving data materialization to compile time.

- **AOT/Trimming Safe:** Zero reflection. Perfect for Native AOT.
- **Zero Runtime Overhead:** Data is baked into the assembly. No parsing at startup.
- **Compile-time Safety:** If the JSON structure breaks, the build fails.

## Setup

### 1. Project Configuration

Add the JSON files as `AdditionalFiles` in your `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Data\*.json" />
</ItemGroup>

```

### 2. Define the Catalog

Mark a `partial` class with the attribute. The generator will fill in the rest.

```csharp
using UniversalDataDriven.SourceGen;

[GenerateFromData("Data/Heroes.json")]
public partial class HeroCatalog { }

```

## Example

### **Input (`Heroes.json`):**

```json
{
 "Mage": { "Health": 80, "Mana": 150 },
 "Tank": { "Health": 200, "Mana": 50 }
}
```

### **Output API:**

The generator produces a `HeroCatalogKind` enum and static accessors within your class:

```csharp
// Direct access
var hp = HeroCatalog.Mage.Health;

// Dictionary lookups (O(1) via FrozenDictionary)
var hero = HeroCatalog.Get(HeroCatalogKind.Tank);

// List all
var allHeroes = HeroCatalog.All;

```

## Features

- **Static Materialization:** Converts JSON nodes directly to C# object initializers.
- **Identity Generation:** Automatically creates enums based on JSON keys.
- **Extensible:** Supports custom readers and type mapping rules if the default logic doesn't fit your schema.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
