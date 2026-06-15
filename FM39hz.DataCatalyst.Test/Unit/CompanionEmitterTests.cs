namespace FM39hz.DataCatalyst.Test.Unit;

using System.Text.Json;
using FluentAssertions;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;
using FM39hz.DataCatalyst.Plugins.Emitters;
using Microsoft.CodeAnalysis;
using Xunit;

public sealed class CompanionEmitterTests {
	private static readonly SchemaInfo FlatSchema = new([
		new SchemaColumn("Name", SchemaType.OfPrimitive("string")),
		new SchemaColumn("Health", SchemaType.OfPrimitive("int")),
		new SchemaColumn("Weight", SchemaType.OfPrimitive("float")),
	]);

	private static readonly SchemaInfo NestedSchema = new([
		new SchemaColumn("Name", SchemaType.OfPrimitive("string")),
		new SchemaColumn("Stats", SchemaType.OfObject([
			new SchemaColumn("Str", SchemaType.OfPrimitive("int")),
			new SchemaColumn("Dex", SchemaType.OfPrimitive("int")),
		])),
	]);

	private static JsonValueModel Json(string raw) => JsonValueModel.From(JsonDocument.Parse(raw).RootElement);

	private static readonly IReadOnlyList<RowData> Rows = [
		new RowData("Potion", new Dictionary<string, JsonValueModel> {
			["Name"] = Json(/*lang=json,strict*/ "{\"x\": \"Potion of Healing\"}").ObjectMembers!["x"],
			["Health"] = Json("50"),
			["Weight"] = Json("0.5"),
		}),
		new RowData("Elixir", new Dictionary<string, JsonValueModel> {
			["Name"] = Json(/*lang=json,strict*/ "{\"x\": \"Elixir of Power\"}").ObjectMembers!["x"],
			["Health"] = Json("200"),
			["Weight"] = Json("0.3"),
		}),
	];

	// --- Registry Discovery ---

	[Fact]
	public void CompanionEmitters_ShouldContainSqliteDataEmitter() => DcPluginRegistry.CompanionEmitters
			.Should().Contain(e => e is SqliteDataEmitter);

	[Fact]
	public void CompanionEmitters_ShouldContainJsonRuntimeDataEmitter() => DcPluginRegistry.CompanionEmitters
			.Should().Contain(e => e is JsonRuntimeDataEmitter);

	[Fact]
	public void CompanionEmitters_ShouldContainModOverlayDataEmitter() => DcPluginRegistry.CompanionEmitters
			.Should().Contain(e => e is ModOverlayDataEmitter);

	[Fact]
	public void CompanionEmitters_Count_ShouldBeThree() => DcPluginRegistry.CompanionEmitters
			.		Should().HaveCount(4);

	// --- SqliteDataEmitter Applicable ---

	[Fact]
	public void SqliteEmitter_Applies_WhenBackendHasSqlite() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		new SqliteDataEmitter().Applies(ctx).Should().BeTrue();
	}

	[Fact]
	public void SqliteEmitter_Applies_WhenBackendIsAll() {
		var ctx = MakeCtx(backend: DataBackend.All);
		new SqliteDataEmitter().Applies(ctx).Should().BeTrue();
	}

	[Fact]
	public void SqliteEmitter_DoesNotApply_WhenBackendIsNone() {
		var ctx = MakeCtx(backend: DataBackend.None);
		new SqliteDataEmitter().Applies(ctx).Should().BeFalse();
	}

	[Fact]
	public void SqliteEmitter_DoesNotApply_WhenBackendIsJsonOnly() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		new SqliteDataEmitter().Applies(ctx).Should().BeFalse();
	}

	// --- JsonRuntimeDataEmitter Applicable ---

	[Fact]
	public void JsonRuntimeEmitter_Applies_WhenBackendHasJson() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		new JsonRuntimeDataEmitter().Applies(ctx).Should().BeTrue();
	}

	[Fact]
	public void JsonRuntimeEmitter_DoesNotApply_WhenBackendIsNone() {
		var ctx = MakeCtx(backend: DataBackend.None);
		new JsonRuntimeDataEmitter().Applies(ctx).Should().BeFalse();
	}

	[Fact]
	public void JsonRuntimeEmitter_DoesNotApply_WhenBackendIsSqliteOnly() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		new JsonRuntimeDataEmitter().Applies(ctx).Should().BeFalse();
	}

	// --- ModOverlayDataEmitter Applicable ---

	[Fact]
	public void ModOverlayEmitter_Applies_WhenModSupportIsTrue() {
		var ctx = MakeCtx(backend: DataBackend.None, modSupport: true);
		new ModOverlayDataEmitter().Applies(ctx).Should().BeTrue();
	}

	[Fact]
	public void ModOverlayEmitter_DoesNotApply_WhenModSupportIsFalse() {
		var ctx = MakeCtx(backend: DataBackend.All, modSupport: false);
		new ModOverlayDataEmitter().Applies(ctx).Should().BeFalse();
	}

	// --- SqliteDataEmitter Generated Code ---

	[Fact]
	public void SqliteEmitter_Emit_ProducesSelectAllConstant() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("SelectAll")
			.And.Contain("SELECT")
			.And.Contain("FROM");
	}

	[Fact]
	public void SqliteEmitter_Emit_ProducesReadRowMethod() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("ReadRow")
			.And.Contain("DbDataReader")
			.And.Contain("reader.GetInt32")
			.And.Contain("reader.GetString")
			.And.Contain("reader.GetFloat");
	}

	[Fact]
	public void SqliteEmitter_Emit_UsesIDbConnection() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("IDbConnection")
			.And.Contain("IDbCommand")
			.And.NotContain("Microsoft.Data.Sqlite");
	}

	[Fact]
	public void SqliteEmitter_Emit_RepoUsesFactory() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("Func<global::System.Data.IDbConnection>")
			.And.Contain("EnsureLoaded")
			.And.Contain("_loadLock");
	}

	[Fact]
	public void SqliteEmitter_Emit_IncludesColumnNamesInOrder() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("Kind") // synthetic Kind column first
			.And.Contain("Name")
			.And.Contain("Health")
			.And.Contain("Weight");
	}

	[Fact]
	public void SqliteEmitter_Emit_ProducesRepositoryClass() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("SqlRepository")
			.And.Contain("IDataRepository");
	}

	// --- JsonRuntimeDataEmitter Generated Code ---

	[Fact]
	public void JsonRuntimeEmitter_Emit_ProducesReadMethod() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		var code = new JsonRuntimeDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("Read(ref global::System.Text.Json.Utf8JsonReader")
			.And.Contain("GetString")
			.And.Contain("GetInt32");
	}

	[Fact]
	public void JsonRuntimeEmitter_Emit_ProducesEnumSwitchForKind() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		var code = new JsonRuntimeDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("case \"Potion\"")
			.And.Contain("case \"Elixir\"")
			.And.Contain("MyDataKind");
	}

	[Fact]
	public void JsonRuntimeEmitter_Emit_ProducesLoadAllMethod() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		var code = new JsonRuntimeDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("LoadAll")
			.And.Contain("File.ReadAllBytes")
			.And.Contain("Utf8JsonReader");
	}

	[Fact]
	public void JsonRuntimeEmitter_Emit_ProducesRepositoryClass() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		var code = new JsonRuntimeDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("JsonRepository")
			.And.Contain("IDataRepository");
	}

	// --- ModOverlayDataEmitter Generated Code ---

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesLoadModsMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("LoadMods")
			.And.Contain("EnumerateFiles")
			.And.Contain("*.json");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesKindToStringSwitch() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("KindToString")
			.And.Contain("case MyDataKind.Potion")
			.And.Contain("case MyDataKind.Elixir");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_IncludesDslReaderIntegration() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("DslReaderRegistry")
			.And.Contain("TryRead");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesTryGetMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("internal static bool TryGet(string");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesGetAllModEntries() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("GetAllModEntries")
			.And.Contain("Array.Empty");
	}

	// --- ModOverlay: AddEntry / RemoveEntry / Clear ---

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesAddEntryMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("public static void AddEntry(string key")
			.And.Contain("_modEntries[key] = entry")
			.And.Contain("OnEntryAdded");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesRemoveEntryMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("public static void RemoveEntry(string key)")
			.And.Contain("_modEntries?.Remove")
			.And.Contain("OnEntryRemoved");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesClearMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("public static void Clear()")
			.And.Contain("_modEntries?.Clear()")
			.And.Contain("OnAllCleared");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_AdapterNotificationPresent() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("DataViewAdapterRegistry.GetAdapters<MyData>")
			.And.Contain("a.OnEntryAdded")
			.And.Contain("a.OnEntryRemoved")
			.And.Contain("a.OnAllCleared");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_LoadModsUsesAddEntry() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("AddEntry(KindToString(item.Kind), item)");
	}

	// --- Component Names ---

	[Fact]
	public void SqliteDataEmitter_Name_ShouldBeSqlite() => new SqliteDataEmitter().Name.Should().Be("Sqlite");

	[Fact]
	public void LuaBridgeEmitter_Name_ShouldBeLuaBridge() {
		new LuaBridgeEmitter().Name.Should().Be("LuaBridge");
	}

	[Fact]
	public void LuaBridgeEmitter_Applies_WhenModSupportIsTrue() {
		var ctx = MakeCtx(modSupport: true);
		new LuaBridgeEmitter().Applies(ctx).Should().BeTrue();
	}

	[Fact]
	public void LuaBridgeEmitter_DoesNotApply_WhenModSupportIsFalse() {
		var ctx = MakeCtx(modSupport: false);
		new LuaBridgeEmitter().Applies(ctx).Should().BeFalse();
	}

	[Fact]
	public void LuaBridgeEmitter_Emit_ProducesRegisterMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new LuaBridgeEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("Register")
			.And.Contain("MoonSharp.Interpreter.Script")
			.And.Contain("_Add\"")
			.And.Contain("_Get\"");
	}

	[Fact]
	public void LuaBridgeEmitter_Emit_GeneratesTypedMethods() {
		var ctx = MakeCtx(modSupport: true);
		var code = new LuaBridgeEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("private static void Add(string key, string Name, int Health, float Weight)")
			.And.Contain("Mod.AddEntry");
	}

	[Fact]
	public void JsonRuntimeDataEmitter_Name_ShouldBeJsonRuntime() => new JsonRuntimeDataEmitter().Name.Should().Be("JsonRuntime");

	[Fact]
	public void ModOverlayDataEmitter_Name_ShouldBeModOverlay() => new ModOverlayDataEmitter().Name.Should().Be("ModOverlay");

	// --- helper ---

	private static DcGenerationContext MakeCtx(DataBackend backend = DataBackend.None, bool modSupport = false, int loadMode = 0, string schemaVersion = "") => new(
		targetFullyQualifiedName: "global::Test.MyData",
		containingNamespace: "Test",
		simpleName: "MyData",
		typeKind: TypeKind.Struct,
		isRecord: false,
		entryPointName: "",
		keyField: "",
		jsonPath: "test.json",
		backend: backend,
		modSupport: modSupport,
		loadMode: loadMode,
		schemaVersion: schemaVersion,
		location: Location.None,
		template: null,
		spc: default);
}
