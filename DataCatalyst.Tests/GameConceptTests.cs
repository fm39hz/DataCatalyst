namespace DataCatalyst.Tests;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Core;
using DataCatalyst.Loaders;
using DataCatalyst.Plugins.GameConcept;
using FluentAssertions;
using Xunit;

// Concept types with [DataConcept] for SourceGen tests
[DataConcept("Item")]
public readonly record struct ItemConcept;

[DataConcept("Enemy")]
public readonly record struct EnemyConcept;

[DataConcept("Money")]
public readonly record struct MoneyConcept;

// Concept types without [DataConcept] for manual registration tests
public readonly record struct TestItemConcept;
public readonly record struct TestEnemyConcept;
public readonly record struct TestMoneyConcept;

public class GameConceptTests : IDisposable {
	private readonly string _tempDir;
	private readonly DataCatalystEnvironment _env;

	public GameConceptTests() {
		_tempDir = Path.Combine(Path.GetTempPath(), "GameConceptTests_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
		_env = new DataCatalystEnvironment();
		_env.Primitives.Register<GameComponent>();
	}

	public void Dispose() {
		if (Directory.Exists(_tempDir)) {
			Directory.Delete(_tempDir, true);
		}
		GC.SuppressFinalize(this);
	}

	private static JsonSerializerOptions CreateOptions() => new() {
		TypeInfoResolver = new DefaultJsonTypeInfoResolver()
	};

	[Fact]
	public void ConceptRegistry_Register_StoresMapping() {
		var registry = new ConceptRegistry();
		registry.Register<TestItemConcept>("Item");
		registry.Register<TestEnemyConcept>("Enemy");

		registry.Count.Should().Be(2);
		registry.IsRegistered<TestItemConcept>().Should().BeTrue();
		registry.IsRegistered<TestEnemyConcept>().Should().BeTrue();
		registry.IsRegistered<TestMoneyConcept>().Should().BeFalse();
	}

	[Fact]
	public void ConceptRegistry_ResolveType_ReturnsCorrectType() {
		var registry = new ConceptRegistry();
		registry.Register<TestItemConcept>("Item");

		var type = registry.ResolveType("Item");
		type.Should().Be(typeof(TestItemConcept));
	}

	[Fact]
	public void ConceptRegistry_ResolveType_ReturnsNullForUnknown() {
		var registry = new ConceptRegistry();

		var type = registry.ResolveType("Unknown");
		type.Should().BeNull();
	}

	[Fact]
	public void ConceptRegistry_ResolveName_ReturnsCorrectName() {
		var registry = new ConceptRegistry();
		registry.Register<TestItemConcept>("Item");

		var name = registry.ResolveName<TestItemConcept>();
		name.Should().Be("Item");
	}

	[Fact]
	public void ConceptRegistry_ResolveName_ReturnsNullForUnregistered() {
		var registry = new ConceptRegistry();

		var name = registry.ResolveName<TestMoneyConcept>();
		name.Should().BeNull();
	}

	[Fact]
	public void ConceptRegistry_ResolveKind_ReturnsKind() {
		var registry = new ConceptRegistry();
		registry.Register<TestItemConcept>("Item", kind: "object");

		var kind = registry.ResolveKind<TestItemConcept>();
		kind.Should().Be("object");
	}

	[Fact]
	public void ConceptRegistry_GetByKind_ReturnsMatchingTypes() {
		var registry = new ConceptRegistry();
		registry.Register<TestItemConcept>("Item", kind: "object");
		registry.Register<TestEnemyConcept>("Enemy", kind: "actor");

		var objects = registry.GetByKind("object");
		objects.Should().Contain(typeof(TestItemConcept));
		objects.Should().NotContain(typeof(TestEnemyConcept));
	}

	[Fact]
	public void GameConceptPlugin_OnCatalogResolved_BuildsConceptCatalogs() {
		// Arrange — entries declare concept via "concept" field
		File.WriteAllText(Path.Combine(_tempDir, "Sword.json"), /*lang=json,strict*/ @"{
			""concept"": ""Item"",
			""GameComponent"": { ""Value"": 100 }
		}");
		File.WriteAllText(Path.Combine(_tempDir, "Shield.json"), /*lang=json,strict*/ @"{
			""concept"": ""Item"",
			""GameComponent"": { ""Value"": 50 }
		}");

		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions(), _env);
		var graph = DataGraphBuilder.Build(loadResult.Entries, env: _env);
		var catalog = DataCatalogBuilder.Resolve(graph, env: _env);

		var plugin = new GameConceptPlugin();
		plugin.Registry.Register<TestItemConcept>("Item");
		var diags = new List<string>();

		// Act
		plugin.OnCatalogResolved(catalog, diags);

		// Assert
		var items = plugin.GetConcept<TestItemConcept>();
		items.Count.Should().Be(2);
		items.ContainsKey("Sword").Should().BeTrue();
		items.ContainsKey("Shield").Should().BeTrue();
	}

	[Fact]
	public void GameConceptPlugin_OnCatalogResolved_HandlesMultipleConcepts() {
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "Sword.json"), /*lang=json,strict*/ @"{
			""concept"": ""Item"",
			""GameComponent"": { ""Value"": 100 }
		}");
		File.WriteAllText(Path.Combine(_tempDir, "Goblin.json"), /*lang=json,strict*/ @"{
			""concept"": ""Enemy"",
			""GameComponent"": { ""Value"": 99 }
		}");

		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions(), _env);
		var graph = DataGraphBuilder.Build(loadResult.Entries, env: _env);
		var catalog = DataCatalogBuilder.Resolve(graph, env: _env);

		var plugin = new GameConceptPlugin();
		plugin.Registry.Register<TestItemConcept>("Item");
		plugin.Registry.Register<TestEnemyConcept>("Enemy");
		var diags = new List<string>();

		// Act
		plugin.OnCatalogResolved(catalog, diags);

		// Assert
		plugin.GetConcept<TestItemConcept>().Count.Should().Be(1);
		plugin.GetConcept<TestEnemyConcept>().Count.Should().Be(1);
	}

	[Fact]
	public void GameConceptPlugin_OnCatalogResolved_IgnoresEntriesWithoutConcept() {
		// Arrange — entry without "concept" field
		File.WriteAllText(Path.Combine(_tempDir, "Sword.json"), /*lang=json,strict*/ @"{
			""GameComponent"": { ""Value"": 100 }
		}");

		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions(), _env);
		var graph = DataGraphBuilder.Build(loadResult.Entries, env: _env);
		var catalog = DataCatalogBuilder.Resolve(graph, env: _env);

		var plugin = new GameConceptPlugin();
		plugin.Registry.Register<TestItemConcept>("Item");
		var diags = new List<string>();

		// Act
		plugin.OnCatalogResolved(catalog, diags);

		// Assert — no concept catalog built (no entries with matching concept)
		// Should not throw, just do nothing
		diags.Should().BeEmpty();
	}

	[Fact]
	public void GameConceptPlugin_GetConcept_ThrowsForUnregisteredConcept() {
		var plugin = new GameConceptPlugin();

		Action act = () => plugin.GetConcept<TestMoneyConcept>();
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*not registered*");
	}

	[Fact]
	public void SourceGen_AutoRegistersConcepts() {
		ConceptRegistry.Default.IsRegistered<ItemConcept>().Should().BeTrue();
		ConceptRegistry.Default.IsRegistered<EnemyConcept>().Should().BeTrue();
		ConceptRegistry.Default.IsRegistered<MoneyConcept>().Should().BeTrue();

		ConceptRegistry.Default.ResolveName<ItemConcept>().Should().Be("Item");
		ConceptRegistry.Default.ResolveName<EnemyConcept>().Should().Be("Enemy");
		ConceptRegistry.Default.ResolveName<MoneyConcept>().Should().Be("Money");
	}
}
