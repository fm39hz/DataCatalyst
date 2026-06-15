namespace FM39hz.DataCatalyst.Test.Unit;

using FluentAssertions;
using FM39hz.DataCatalyst.Test.Support;
using Microsoft.CodeAnalysis;
using Xunit;

public sealed class IntegrationTests : IntegrationTestBase {

	// === Core data gen ===

	[Fact]
	public void CoreGen_ProducesEnumAndFrozenDictionary() {
		var json = /*lang=json,strict*/ """
			{ "Potion": { "Health": 50 }, "Elixir": { "Health": 200 } }
			""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json")]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
		var code = FindSource(sources, "ItemKind");
		code.Should().NotBeNull();
		code!.Should().Contain("enum ItemKind")
			.And.Contain("Potion")
			.And.Contain("Elixir")
			.And.Contain("FrozenDictionary<ItemKind, Item>")
			.And.Contain("public static Item Get(ItemKind kind) => All[kind]");
	}

	// === ModSupport gen ===

	[Fact]
	public void ModSupportGen_ProducesModClass() {
		var json = /*lang=json,strict*/ """
			{ "Potion": { "Health": 50 } }
			""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json", ModSupport = true)]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

		var mainSrc = FindSource(sources, "enum ItemKind");
		mainSrc.Should().NotBeNull();
		mainSrc!.Should().Contain("ItemMod.TryGet")
			.And.Contain("DataBackendSelector");

		var modSrc = FindSource(sources, "public static void AddEntry(string key");
		modSrc.Should().NotBeNull();
		modSrc!.Should().Contain("AddEntry(string key")
			.And.Contain("RemoveEntry(string key)")
			.And.Contain("Clear()")
			.And.Contain("OnEntryAdded")
			.And.Contain("DataViewAdapterRegistry");
	}

	// === Backend gen (Json) ===

	[Fact]
	public void BackendJsonGen_ProducesJsonReader() {
		var json = /*lang=json,strict*/ """
			{ "Potion": { "Health": 50 } }
			""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json", Backend = 1)]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

		var jsonSrc = FindSource(sources, "Read(ref global::System.Text.Json.Utf8JsonReader");
		jsonSrc.Should().NotBeNull();
		jsonSrc!.Should().Contain("Utf8JsonReader")
			.And.Contain("LoadAll")
			.And.Contain("JsonRepository");
	}

	// === Backend gen (Sqlite) ===

	[Fact]
	public void BackendSqliteGen_ProducesSqlReader() {
		var json = /*lang=json,strict*/ """
			{ "Potion": { "Health": 50 } }
			""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json", Backend = 2)]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

		var sqlSrc = FindSource(sources, "ItemSql.CreateSelectAllCommand");
		sqlSrc.Should().NotBeNull();
		sqlSrc!.Should().Contain("IDbConnection")
			.And.Contain("IDbCommand")
			.And.Contain("DbDataReader")
			.And.Contain("ReadRow");
	}

	// === All backends + ModSupport ===

	[Fact]
	public void AllBackendsWithModSupport_ProducesFourFiles() {
		var json = /*lang=json,strict*/ """
			{ "Potion": { "Health": 50 } }
			""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json", Backend = 3, ModSupport = true)]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

		var hasCore = sources.Any(s => s.Contains("enum ItemKind"));
		var hasSql = sources.Any(s => s.Contains("ItemSql"));
		var hasJson = sources.Any(s => s.Contains("ItemJson"));
		var hasMod = sources.Any(s => s.Contains("ItemMod"));
		(hasCore && hasSql && hasJson && hasMod).Should().BeTrue();
	}

	// === Runtime source file ===

	[Fact]
	public void RuntimeSource_ContainsAllContracts() {
		var json = /*lang=json,strict*/ """{ "Potion": { "Health": 50 } }""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json")]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

		var runtime = FindSource(sources, "IDataRepository<");
		runtime.Should().NotBeNull();
		runtime!.Should().Contain("IDataViewAdapter<")
			.And.Contain("DataViewAdapterRegistry")
			.And.Contain("IModPlugin")
			.And.Contain("IModGameContext")
			.And.Contain("ServiceRegistry")
			.And.Contain("ModGameContext")
			.And.Contain("PluginRegistry")
			.And.Contain("DataBackendSelector")
			.And.Contain("DataBackendConst")
			.And.Contain("DslReaderRegistry");
	}

	// === Diagnostics: non-partial ===

	[Fact]
	public void NonPartialType_ReportsError() {
		var json = /*lang=json,strict*/ """{ "Potion": { "Health": 50 } }""";
		var (_, diags, _) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json")]
			public struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Should().Contain(d => d.Id == "DC0005");
	}

	// === Diagnostics: bad JSON ===

	[Fact]
	public void InvalidJson_ReportsError() {
		var (_, diags, _) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("bad.json")]
			public partial struct Item { }
			""",
			new TestAdditionalText("bad.json", "not json at all {{"));

		diags.Should().Contain(d => d.Id == "DC0002");
	}

	// === Attribute file ===

	[Fact]
	public void AttributeFile_ContainsBothAttributes() {
		var json = /*lang=json,strict*/ """{ "Potion": { "Health": 50 } }""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json")]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

		var attr = FindSource(sources, "CatalystDataAttribute");
		attr.Should().NotBeNull();
		attr!.Should().Contain("CatalystDataAttribute")
			.And.Contain("ModPluginAttribute")
			.And.Contain("KeyField")
			.And.Contain("Backend")
			.And.Contain("ModSupport");
	}

	[Fact]
	public void CoreGen_ProducesQueryAndFind() {
		var json = /*lang=json,strict*/ """{ "Potion": { "Health": 50 } }""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json")]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
		var core = FindSource(sources, "enum ItemKind");
		core.Should().NotBeNull();
		core!.Should().Contain("Query")
			.And.Contain("Find")
			.And.Contain("Func<Item, bool>");
	}

	[Fact]
	public void ModOverlay_ProducesAddRange() {
		var json = /*lang=json,strict*/ """{ "Potion": { "Health": 50 } }""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json", ModSupport = true)]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
		var modSrc = FindSource(sources, "public static void AddEntry(string key");
		modSrc.Should().NotBeNull();
		modSrc!.Should().Contain("AddRange")
			.And.Contain("string Key, Item Value");
	}

	[Fact]
	public void RuntimeSource_ContainsDataRefAndCatalogRegistry() {
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json")]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", /*lang=json,strict*/ """{ "Potion": { "Health": 50 } }"""));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
		var runtime = FindSource(sources, "IDataRepository<");
		runtime.Should().NotBeNull();
		runtime!.Should().Contain("DataRef<")
			.And.Contain("CatalogRegistry");
	}

	[Fact]
	public void CoreGen_RegistersCatalog() {
		var json = /*lang=json,strict*/ """{ "Potion": { "Health": 50 } }""";
		var (_, diags, sources) = RunGenerator("""
			using FM39hz.DataCatalyst;
			[CatalystData("test.json")]
			public partial struct Item { }
			""",
			new TestAdditionalText("test.json", json));

		diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
		var core = FindSource(sources, "enum ItemKind");
		core.Should().NotBeNull();
		core!.Should().Contain("ModuleInitializer")
			.And.Contain("RegisterCatalog")
			.And.Contain("CatalogRegistry.Register<Item>");
	}
}
