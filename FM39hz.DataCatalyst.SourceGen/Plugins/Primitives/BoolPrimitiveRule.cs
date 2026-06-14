namespace FM39hz.DataCatalyst.Plugins.Primitives;

using System.Runtime.CompilerServices;
using System.Text.Json;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;

/// <summary>
///     Maps <see cref="JsonValueKind.True" />/<see cref="JsonValueKind.False" /> to C# <c>bool</c>.
///     Non-widenable: a column that mixes bool with any other primitive is rejected at the schema level.
/// </summary>
[DcPlugin(typeof(IPrimitiveTypeRule))]
internal sealed class BoolPrimitiveRule : IPrimitiveTypeRule {
	[ModuleInitializer]
	internal static void Register() => DcPluginRegistry.Register(new BoolPrimitiveRule());

	public string Name => "bool";
	public int Rank => -1;
	public JsonValueKind BoundKind => JsonValueKind.True;

	public bool TryInfer(JsonValueModel value) =>
		value.Kind == JsonValueKind.True || value.Kind == JsonValueKind.False;

	public string EmitLiteral(JsonValueModel value) =>
		value.Kind == JsonValueKind.True ? "true" : value.Kind == JsonValueKind.False ? "false" : "false";

	public string EmitDefault() => "false";
}
