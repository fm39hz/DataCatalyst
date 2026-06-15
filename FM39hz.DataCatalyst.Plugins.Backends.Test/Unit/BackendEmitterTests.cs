namespace FM39hz.DataCatalyst.Plugins.Backends.Test.Unit;

using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;
using FM39hz.DataCatalyst.Plugins.Emitters;
using Microsoft.CodeAnalysis;
using Xunit;

public sealed class BackendEmitterTests {
	private static readonly SchemaInfo FlatSchema = new([
		new SchemaColumn("Name", SchemaType.OfPrimitive("string")),
		new SchemaColumn("Health", SchemaType.OfPrimitive("int")),
		new SchemaColumn("Weight", SchemaType.OfPrimitive("float")),
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

	// --- SqliteDataEmitter ---

	[Fact]
	public void SqliteDataEmitter_Name_ShouldBeSqlite() => new SqliteDataEmitter().Name.Should().Be("Sqlite");

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

	[Fact]
	public void SqliteEmitter_Emit_ProducesSelectAllConstant() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("SelectAll").And.Contain("SELECT").And.Contain("FROM");
	}

	[Fact]
	public void SqliteEmitter_Emit_ProducesReadRowMethod() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("ReadRow").And.Contain("DbDataReader")
			.And.Contain("reader.GetInt32").And.Contain("reader.GetString")
			.And.Contain("reader.GetFloat");
	}

	[Fact]
	public void SqliteEmitter_Emit_UsesIDbConnection() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("IDbConnection").And.Contain("IDbCommand")
			.And.NotContain("Microsoft.Data.Sqlite");
	}

	[Fact]
	public void SqliteEmitter_Emit_RepoUsesFactory() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("Func<global::System.Data.IDbConnection>")
			.And.Contain("EnsureLoaded").And.Contain("_loadLock");
	}

	[Fact]
	public void SqliteEmitter_Emit_IncludesColumnNamesInOrder() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("Kind").And.Contain("Name")
			.And.Contain("Health").And.Contain("Weight");
	}

	[Fact]
	public void SqliteEmitter_Emit_ProducesRepositoryClass() {
		var ctx = MakeCtx(backend: DataBackend.Sqlite);
		var code = new SqliteDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("SqlRepository").And.Contain("IDataRepository");
	}

	// --- JsonRuntimeDataEmitter ---

	[Fact]
	public void JsonRuntimeDataEmitter_Name_ShouldBeJsonRuntime() => new JsonRuntimeDataEmitter().Name.Should().Be("JsonRuntime");

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

	[Fact]
	public void JsonRuntimeEmitter_Emit_ProducesReadMethod() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		var code = new JsonRuntimeDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("Read(ref global::System.Text.Json.Utf8JsonReader")
			.And.Contain("GetString").And.Contain("GetInt32");
	}

	[Fact]
	public void JsonRuntimeEmitter_Emit_ProducesEnumSwitchForKind() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		var code = new JsonRuntimeDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("case \"Potion\"").And.Contain("case \"Elixir\"")
			.And.Contain("MyDataKind");
	}

	[Fact]
	public void JsonRuntimeEmitter_Emit_ProducesLoadAllMethod() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		var code = new JsonRuntimeDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("LoadAll").And.Contain("File.ReadAllBytes")
			.And.Contain("Utf8JsonReader");
	}

	[Fact]
	public void JsonRuntimeEmitter_Emit_ProducesRepositoryClass() {
		var ctx = MakeCtx(backend: DataBackend.Json);
		var code = new JsonRuntimeDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("JsonRepository").And.Contain("IDataRepository");
	}

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
