namespace DataCatalyst.Tests;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Core;
using DataCatalyst.Loaders;
using DataCatalyst.Plugins.ConceptDomain;
using FluentAssertions;
using Xunit;

// Tag types with [DataConcept] for SourceGen tests
[DataConcept("Item")]
public readonly record struct ItemTag;

[DataConcept("Enemy")]
public readonly record struct EnemyTag;

[DataConcept("Money")]
public readonly record struct MoneyTag;

// Tag types without [DataConcept] for manual registration tests
public readonly record struct TestItemTag;
public readonly record struct TestEnemyTag;
public readonly record struct TestMoneyTag;

public class ConceptDomainTests : IDisposable {
	private readonly string _tempDir;
	private readonly DataCatalystEnvironment _env;

	public ConceptDomainTests() {
		_tempDir = Path.Combine(Path.GetTempPath(), "ConceptDomainTests_" + Guid.NewGuid().ToString("N"));
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
		registry.Register<TestItemTag>("Item");
		registry.Register<TestEnemyTag>("Enemy");

		registry.Count.Should().Be(2);
		registry.IsRegistered<TestItemTag>().Should().BeTrue();
		registry.IsRegistered<TestEnemyTag>().Should().BeTrue();
		registry.IsRegistered<TestMoneyTag>().Should().BeFalse();
	}

	[Fact]
	public void ConceptRegistry_ResolveType_ReturnsCorrectType() {
		var registry = new ConceptRegistry();
		registry.Register<TestItemTag>("Item");

		var type = registry.ResolveType("Item");
		type.Should().Be(typeof(TestItemTag));
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
		registry.Register<TestItemTag>("Item");

		var name = registry.ResolveName<TestItemTag>();
		name.Should().Be("Item");
	}

	[Fact]
	public void ConceptRegistry_ResolveName_ReturnsNullForUnregistered() {
		var registry = new ConceptRegistry();

		var name = registry.ResolveName<TestMoneyTag>();
		name.Should().BeNull();
	}

	[Fact]
	public void ConceptDomainPlugin_OnCatalogResolved_BuildsConceptCatalogs() {
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "Sword.json"), /*lang=json,strict*/ @"{
			""GameComponent"": { ""Value"": 100 }
		}");
		File.WriteAllText(Path.Combine(_tempDir, "Shield.json"), /*lang=json,strict*/ @"{
			""GameComponent"": { ""Value"": 50 }
		}");

		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions(), _env);
		var graph = DataGraphBuilder.Build(loadResult.Entries, env: _env);
		var catalog = DataCatalogBuilder.Resolve(graph, env: _env);

		var plugin = new ConceptDomainPlugin();
		plugin.Registry.Register<TestItemTag>("Item");
		plugin.RegisterEntries("Item", "Sword", "Shield");

		var diags = new List<string>();

		// Act
		plugin.OnCatalogResolved(catalog, diags);

		// Assert
		diags.Should().NotContain(d => d.Contains("Item"));
		var items = plugin.GetConcept<TestItemTag>();
		items.Count.Should().Be(2);
	}

	[Fact]
	public void ConceptDomainPlugin_OnCatalogResolved_DiagnosesMissingEntries() {
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "Sword.json"), /*lang=json,strict*/ @"{
			""GameComponent"": { ""Value"": 100 }
		}");

		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions(), _env);
		var graph = DataGraphBuilder.Build(loadResult.Entries, env: _env);
		var catalog = DataCatalogBuilder.Resolve(graph, env: _env);

		var plugin = new ConceptDomainPlugin();
		plugin.Registry.Register<TestItemTag>("Item");
		plugin.RegisterEntries("Item", "Sword", "NonExistent");

		var diags = new List<string>();

		// Act
		plugin.OnCatalogResolved(catalog, diags);

		// Assert
		diags.Should().Contain(d => d.Contains("NonExistent") && d.Contains("not found"));
	}

	[Fact]
	public void ConceptDomainPlugin_OnCatalogResolved_DiagnosesUnregisteredConcepts() {
		// Arrange
		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions(), _env);
		var graph = DataGraphBuilder.Build(loadResult.Entries, env: _env);
		var catalog = DataCatalogBuilder.Resolve(graph, env: _env);

		var plugin = new ConceptDomainPlugin();
		plugin.Registry.Register<TestItemTag>("Item");

		var diags = new List<string>();

		// Act
		plugin.OnCatalogResolved(catalog, diags);

		// Assert
		diags.Should().Contain(d => d.Contains("Item") && d.Contains("has no entries"));
	}

	[Fact]
	public void ConceptDomainPlugin_LoadConcepts_LoadsFromFile() {
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "Sword.json"), /*lang=json,strict*/ @"{
			""GameComponent"": { ""Value"": 100 }
		}");
		File.WriteAllText(Path.Combine(_tempDir, "Shield.json"), /*lang=json,strict*/ @"{
			""GameComponent"": { ""Value"": 50 }
		}");
		File.WriteAllText(Path.Combine(_tempDir, "Goblin.json"), /*lang=json,strict*/ @"{
			""GameComponent"": { ""Value"": 25 }
		}");

		var conceptsFile = Path.Combine(_tempDir, "concepts.json");
		File.WriteAllText(conceptsFile, /*lang=json,strict*/ @"{
			""Item"": [""Sword"", ""Shield""],
			""Enemy"": [""Goblin""]
		}");

		var loadResult = JsonDataLoader.LoadDirectory(_tempDir, CreateOptions(), _env);
		var graph = DataGraphBuilder.Build(loadResult.Entries, env: _env);
		var catalog = DataCatalogBuilder.Resolve(graph, env: _env);

		var plugin = new ConceptDomainPlugin();
		plugin.Registry.Register<TestItemTag>("Item");
		plugin.Registry.Register<TestEnemyTag>("Enemy");
		plugin.LoadConcepts(conceptsFile);

		var diags = new List<string>();

		// Act
		plugin.OnCatalogResolved(catalog, diags);

		// Assert
		var items = plugin.GetConcept<TestItemTag>();
		items.Count.Should().Be(2);
	}

	[Fact]
	public void ConceptDomainPlugin_GetConcept_ThrowsForUnregisteredConcept() {
		var plugin = new ConceptDomainPlugin();

		Action act = () => plugin.GetConcept<TestMoneyTag>();
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*not registered*");
	}

	[Fact]
	public void SourceGen_AutoRegistersConcepts() {
		// SourceGen should auto-register ItemTag, EnemyTag, MoneyTag via [DataConcept] attribute
		ConceptRegistry.Default.IsRegistered<ItemTag>().Should().BeTrue();
		ConceptRegistry.Default.IsRegistered<EnemyTag>().Should().BeTrue();
		ConceptRegistry.Default.IsRegistered<MoneyTag>().Should().BeTrue();

		ConceptRegistry.Default.ResolveName<ItemTag>().Should().Be("Item");
		ConceptRegistry.Default.ResolveName<EnemyTag>().Should().Be("Enemy");
		ConceptRegistry.Default.ResolveName<MoneyTag>().Should().Be("Money");
	}
}
