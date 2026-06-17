using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Core;
using DataCatalyst.Loaders;
using FluentAssertions;
using Xunit;

namespace DataCatalyst.Tests {
	public struct GameComponent : IComponent {
		public int Value { get; set; }
	}
} // namespace DataCatalyst.Tests

namespace ModNamespace {
	using DataCatalyst.Tests;
	public struct GameComponent : IComponent {
		public string Name { get; set; }
	}
}

namespace DataCatalyst.Tests {

public class JsonDataLoaderTests : IDisposable {
	private readonly string _tempDir;

	public JsonDataLoaderTests() {
		_tempDir = Path.Combine(Path.GetTempPath(), "DataCatalystTests_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
		PrimitiveRegistry.Clear();
	}

	public void Dispose() {
		if (Directory.Exists(_tempDir)) {
			Directory.Delete(_tempDir, true);
		}
		PrimitiveRegistry.Clear();
		GC.SuppressFinalize(this);
	}

	private static JsonSerializerOptions CreateAotOptions() {
		return new JsonSerializerOptions {
			TypeInfoResolver = new DefaultJsonTypeInfoResolver()
		};
	}

	[Fact]
	public void LoadDirectory_WithSerializerOptions_Succeeds() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();
		var jsonPath = Path.Combine(_tempDir, "hero.json");
		File.WriteAllText(jsonPath, @"{
			""GameComponent"": {
				""Value"": 123
			}
		}");

		var options = CreateAotOptions();

		// Act
		var result = JsonDataLoader.LoadDirectory(_tempDir, options);

		// Assert
		result.Diagnostics.Should().BeEmpty();
		result.Entries.Should().ContainSingle();
		var entry = result.Entries[0];
		entry.Key.Should().Be("hero");
		entry.Get<GameComponent>().Value.Should().Be(123);
	}

	[Fact]
	public void LoadDirectory_AmbiguousComponent_RequiresFullyQualifiedName() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();
		PrimitiveRegistry.Register<ModNamespace.GameComponent>();

		var jsonPath = Path.Combine(_tempDir, "hero.json");
		File.WriteAllText(jsonPath, @"{
			""GameComponent"": {
				""Value"": 123
			}
		}");

		var options = CreateAotOptions();

		// Act
		var result = JsonDataLoader.LoadDirectory(_tempDir, options);

		// Assert
		result.Entries.Should().ContainSingle();
		result.Entries[0].Components.Should().BeEmpty(); // Ambiguous component was skipped
		result.Diagnostics.Should().ContainSingle().Which.Should().Contain("Ambiguous component short name");
	}

	[Fact]
	public void LoadDirectory_AmbiguousComponentResolvedByFullName() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();
		PrimitiveRegistry.Register<ModNamespace.GameComponent>();

		var jsonPath = Path.Combine(_tempDir, "hero.json");
		File.WriteAllText(jsonPath, @"{
			""DataCatalyst.Tests.GameComponent"": {
				""Value"": 456
			},
			""ModNamespace.GameComponent"": {
				""Name"": ""ModHero""
			}
		}");

		var options = CreateAotOptions();

		// Act
		var result = JsonDataLoader.LoadDirectory(_tempDir, options);

		// Assert
		result.Diagnostics.Should().BeEmpty();
		result.Entries.Should().ContainSingle();
		var entry = result.Entries[0];
		entry.Get<GameComponent>().Value.Should().Be(456);
		entry.Get<ModNamespace.GameComponent>().Name.Should().Be("ModHero");
	}

	[Fact]
	public void LoadDirectory_RecordsDiagnosticsForMalformedJson() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();
		var jsonPath = Path.Combine(_tempDir, "malformed.json");
		File.WriteAllText(jsonPath, "{ invalid json }");

		var options = CreateAotOptions();

		// Act
		var result = JsonDataLoader.LoadDirectory(_tempDir, options);

		// Assert
		result.Entries.Should().BeEmpty();
		result.Diagnostics.Should().ContainSingle(d => d.Contains("Failed to load file"));
	}

	[Fact]
	public void DataGraphBuilder_DuplicateKeys_MergesComponentsDeterministically() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();
		PrimitiveRegistry.Register<OtherStruct>();

		var entries = new List<DataEntry>();

		// Base entry
		var baseEntry = new DataEntry("enemy");
		baseEntry.Set(new GameComponent { Value = 10 });
		baseEntry.SourceFile = "Base/enemy.json";
		entries.Add(baseEntry);

		// Mod entry overriding base entry component and adding another component
		var modEntry = new DataEntry("enemy");
		modEntry.Set(new GameComponent { Value = 25 }); // Overrides base value
		modEntry.Set(new OtherStruct { Y = 99 });       // Adds new component
		modEntry.SourceFile = "Mod/enemy.json";
		entries.Add(modEntry);

		var diagnostics = new List<string>();

		// Act
		var graph = DataGraphBuilder.Build(entries, diagnostics);

		// Assert
		diagnostics.Should().ContainSingle(d => d.Contains("overrides/merges components of existing entry"));
		graph.Entries.Should().ContainKey("enemy");

		var resolvedEntry = graph.Entries["enemy"];
		resolvedEntry.Get<GameComponent>().Value.Should().Be(25);
		resolvedEntry.Get<OtherStruct>().Y.Should().Be(99);
	}

	[Fact]
	public void LoadDirectory_WithInstanceRegistry_Succeeds() {
		// Arrange
		var registry = new DataRegistry();
		registry.RegisterComponent<GameComponent>();

		var jsonPath = Path.Combine(_tempDir, "hero.json");
		File.WriteAllText(jsonPath, @"{
			""GameComponent"": {
				""Value"": 123
			}
		}");

		var options = CreateAotOptions();

		// Act
		var result = JsonDataLoader.LoadDirectory(_tempDir, registry, options);

		// Assert
		result.Diagnostics.Should().BeEmpty();
		result.Entries.Should().ContainSingle();
		var entry = result.Entries[0];
		entry.Key.Should().Be("hero");
		entry.Get<GameComponent>().Value.Should().Be(123);
	}
}
} // namespace DataCatalyst.Tests
