namespace DataCatalyst.Tests;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Core;
using DataCatalyst.Loaders;
using DataCatalyst.Plugins.GameConcept;
using CoreConcept = DataCatalyst.Core.Concept;
using FluentAssertions;
using Xunit;

// Concept types with [DataConcept] for SourceGen tests — nested under Concept class
// (matches SourceGen-generated output from concepts.json)
public static partial class Concept {
	[DataConcept("Item")] public readonly partial struct Item;
	[DataConcept("Enemy")] public readonly partial struct Enemy;
	[DataConcept("Money")] public readonly partial struct Money;
}

// Concept types without [DataConcept] for manual registration tests
public readonly record struct TestItemConcept;
public readonly record struct TestEnemyConcept;
public readonly record struct TestMoneyConcept;
public readonly record struct TestKindA;
public readonly record struct TestKindB;

public class GameConceptTests : IDisposable {
	private readonly string _tempDir;
	private readonly DataCatalystEnvironment _env;

	public GameConceptTests() {
		_tempDir = Path.Combine(Path.GetTempPath(), "GameConceptTests_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
		_env = new DataCatalystEnvironment();
		_env.Primitives.Register<GameComponent>();
		_env.Primitives.Register<CoreConcept>();
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
		registry.Register<TestItemConcept>("Item", kind: typeof(TestKindA));

		var kind = registry.ResolveKind<TestItemConcept>();
		kind.Should().Be(typeof(TestKindA));
	}

	[Fact]
	public void ConceptRegistry_GetByKind_ReturnsMatchingTypes() {
		var registry = new ConceptRegistry();
		registry.Register<TestItemConcept>("Item", kind: typeof(TestKindA));
		registry.Register<TestEnemyConcept>("Enemy", kind: typeof(TestKindB));

		var objects = registry.GetByKind<TestKindA>();
		objects.Should().Contain(typeof(TestItemConcept));
		objects.Should().NotContain(typeof(TestEnemyConcept));
	}

	[Fact]
	public void GameConceptPlugin_OnCatalogResolved_BuildsConceptCatalogs() {
		// Arrange — entries declare concept via "Concept" field
		File.WriteAllText(Path.Combine(_tempDir, "Sword.json"), /*lang=json,strict*/ @"{
			""Concept"": ""Item"",
			""GameComponent"": { ""Value"": 100 }
		}");
		File.WriteAllText(Path.Combine(_tempDir, "Shield.json"), /*lang=json,strict*/ @"{
			""Concept"": ""Item"",
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
		items.ContainsKey(catalog.GetEntryId("Sword")).Should().BeTrue();
		items.ContainsKey(catalog.GetEntryId("Shield")).Should().BeTrue();
	}

	[Fact]
	public void GameConceptPlugin_OnCatalogResolved_HandlesMultipleConcepts() {
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "Sword.json"), /*lang=json,strict*/ @"{
			""Concept"": ""Item"",
			""GameComponent"": { ""Value"": 100 }
		}");
		File.WriteAllText(Path.Combine(_tempDir, "Goblin.json"), /*lang=json,strict*/ @"{
			""Concept"": ""Enemy"",
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
		// Arrange — entry without "Concept" field
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
		ConceptRegistry.Default.IsRegistered<Concept.Item>().Should().BeTrue();
		ConceptRegistry.Default.IsRegistered<Concept.Enemy>().Should().BeTrue();
		ConceptRegistry.Default.IsRegistered<Concept.Money>().Should().BeTrue();

		ConceptRegistry.Default.ResolveName<Concept.Item>().Should().Be("Item");
		ConceptRegistry.Default.ResolveName<Concept.Enemy>().Should().Be("Enemy");
		ConceptRegistry.Default.ResolveName<Concept.Money>().Should().Be("Money");
	}
}
