namespace DataCatalyst.Tests;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Core;
using FluentAssertions;
using Loaders;
using Xunit;

public class IntegrationTests : IDisposable {
	private readonly string _tempDir;

	public IntegrationTests() {
		_tempDir = Path.Combine(Path.GetTempPath(), "DataCatalystIntegrationTests_" + Guid.NewGuid().ToString("N"));
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

	private static JsonSerializerOptions CreateOptions() => new() {
		TypeInfoResolver = new DefaultJsonTypeInfoResolver()
	};

	[Fact]
	public void EndToEnd_LoadAndResolveInheritance_FlattensCorrectly() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();
		PrimitiveRegistry.Register<OtherStruct>();

		// Write BaseEntity.json
		File.WriteAllText(Path.Combine(_tempDir, "BaseEntity.json"), /*lang=json,strict*/ @"{
			""GameComponent"": {
				""Value"": 10
			}
		}");

		// Write BaseMonster.json inheriting BaseEntity
		File.WriteAllText(Path.Combine(_tempDir, "BaseMonster.json"), /*lang=json,strict*/ @"{
			""inherits"": [ ""BaseEntity"" ],
			""OtherStruct"": {
				""Y"": 20
			}
		}");

		// Write Goblin.json inheriting BaseMonster and overriding GameComponent
		File.WriteAllText(Path.Combine(_tempDir, "Goblin.json"), /*lang=json,strict*/ @"{
			""inherits"": [ ""BaseMonster"" ],
			""GameComponent"": {
				""Value"": 99
			}
		}");

		// Act
		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions());
		loadResult.Diagnostics.Should().BeEmpty();

		var graph = DataGraphBuilder.Build(loadResult.Entries);
		var catalog = DataCatalogBuilder.Resolve(graph);

		// Assert
		catalog.ContainsKey("Goblin").Should().BeTrue();
		catalog.ContainsKey("BaseMonster").Should().BeTrue();
		catalog.ContainsKey("BaseEntity").Should().BeTrue();

		// Goblin should have merged components
		var goblin = catalog.Entries["Goblin"];
		goblin.Get<GameComponent>().Value.Should().Be(99); // Overridden
		goblin.Get<OtherStruct>().Y.Should().Be(20);       // Inherited from BaseMonster
	}

	[Fact]
	public void EndToEnd_CyclicInheritance_ThrowsException() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();

		File.WriteAllText(Path.Combine(_tempDir, "LoopA.json"), /*lang=json,strict*/ @"{
			""inherits"": [ ""LoopB"" ]
		}");

		File.WriteAllText(Path.Combine(_tempDir, "LoopB.json"), /*lang=json,strict*/ @"{
			""inherits"": [ ""LoopA"" ]
		}");

		// Act
		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions());
		var graph = DataGraphBuilder.Build(loadResult.Entries);

		Action act = () => DataCatalogBuilder.Resolve(graph);

		// Assert
		act.Should().Throw<InvalidOperationException>().WithMessage("*Cycle detected*");
	}

	[Fact]
	public void EndToEnd_SelfLoopInheritance_ThrowsException() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();

		File.WriteAllText(Path.Combine(_tempDir, "SelfLoop.json"), /*lang=json,strict*/ @"{
			""inherits"": [ ""SelfLoop"" ]
		}");

		// Act
		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions());
		var graph = DataGraphBuilder.Build(loadResult.Entries);

		Action act = () => DataCatalogBuilder.Resolve(graph);

		// Assert
		act.Should().Throw<InvalidOperationException>().WithMessage("*Cycle detected*");
	}

	[Fact]
	public void EndToEnd_DeepCyclicInheritance_ThrowsException() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();

		// A -> B -> C -> B (Nested loop starting at B)
		File.WriteAllText(Path.Combine(_tempDir, "LoopA.json"), /*lang=json,strict*/ @"{
			""inherits"": [ ""LoopB"" ]
		}");

		File.WriteAllText(Path.Combine(_tempDir, "LoopB.json"), /*lang=json,strict*/ @"{
			""inherits"": [ ""LoopC"" ]
		}");

		File.WriteAllText(Path.Combine(_tempDir, "LoopC.json"), /*lang=json,strict*/ @"{
			""inherits"": [ ""LoopB"" ]
		}");

		// Act
		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions());
		var graph = DataGraphBuilder.Build(loadResult.Entries);

		Action act = () => DataCatalogBuilder.Resolve(graph);

		// Assert
		act.Should().Throw<InvalidOperationException>().WithMessage("*Cycle detected*");
	}

	[Fact]
	public void EndToEnd_MissingInheritanceParent_IsIgnoredWithWarnings() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();

		File.WriteAllText(Path.Combine(_tempDir, "Goblin.json"), /*lang=json,strict*/ @"{
			""inherits"": [ ""NonExistentParent"" ],
			""GameComponent"": {
				""Value"": 1
			}
		}");

		// Act
		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions());
		var graph = DataGraphBuilder.Build(loadResult.Entries);
		var catalog = DataCatalogBuilder.Resolve(graph);

		// Assert
		catalog.ContainsKey("Goblin").Should().BeTrue();
		catalog.Get<GameComponent>("Goblin").Value.Should().Be(1);
	}
}

// namespace DataCatalyst.Tests
