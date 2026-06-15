namespace FM39hz.DataCatalyst.Plugins.Modding.Test.Unit;

using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;
using FM39hz.DataCatalyst.Plugins.Emitters;
using Microsoft.CodeAnalysis;
using Xunit;

public sealed class ModdingEmitterTests {
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

	// --- ModOverlayDataEmitter ---

	[Fact]
	public void ModOverlayDataEmitter_Name_ShouldBeModOverlay() => new ModOverlayDataEmitter().Name.Should().Be("ModOverlay");

	[Fact]
	public void ModOverlayEmitter_Applies_WhenModSupportIsTrue() {
		var ctx = MakeCtx(modSupport: true);
		new ModOverlayDataEmitter().Applies(ctx).Should().BeTrue();
	}

	[Fact]
	public void ModOverlayEmitter_DoesNotApply_WhenModSupportIsFalse() {
		var ctx = MakeCtx(modSupport: false);
		new ModOverlayDataEmitter().Applies(ctx).Should().BeFalse();
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesLoadModsMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("LoadMods").And.Contain("EnumerateFiles").And.Contain("*.json");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesKindToStringSwitch() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("KindToString")
			.And.Contain("case MyDataKind.Potion").And.Contain("case MyDataKind.Elixir");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_IncludesDslReaderIntegration() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("DslReaderRegistry").And.Contain("TryRead");
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
		code.Should().Contain("GetAllModEntries").And.Contain("Array.Empty");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesAddEntryMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("public static void AddEntry(string key")
			.And.Contain("_modEntries[key] = entry").And.Contain("OnEntryAdded");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesRemoveEntryMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("public static void RemoveEntry(string key)")
			.And.Contain("_modEntries?.Remove").And.Contain("OnEntryRemoved");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_ProducesClearMethod() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("public static void Clear()")
			.And.Contain("_modEntries?.Clear()").And.Contain("OnAllCleared");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_AdapterNotificationPresent() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("DataViewAdapterRegistry.GetAdapters<MyData>")
			.And.Contain("a.OnEntryAdded").And.Contain("a.OnEntryRemoved")
			.And.Contain("a.OnAllCleared");
	}

	[Fact]
	public void ModOverlayEmitter_Emit_LoadModsUsesAddEntry() {
		var ctx = MakeCtx(modSupport: true);
		var code = new ModOverlayDataEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("AddEntry(KindToString(item.Kind), item)");
	}

	// --- LuaBridgeEmitter ---

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
		code.Should().Contain("Register").And.Contain("MoonSharp.Interpreter.Script")
			.And.Contain("_Add\"").And.Contain("_Get\"");
	}

	[Fact]
	public void LuaBridgeEmitter_Emit_GeneratesTypedMethods() {
		var ctx = MakeCtx(modSupport: true);
		var code = new LuaBridgeEmitter().Emit(Rows, FlatSchema, ctx);
		code.Should().Contain("private static void Add(string key, string Name, int Health, float Weight)")
			.And.Contain("Mod.AddEntry");
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
