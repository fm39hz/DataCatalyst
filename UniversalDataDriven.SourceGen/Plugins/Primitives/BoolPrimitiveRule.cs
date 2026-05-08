namespace UniversalDataDriven.Plugins.Primitives;

using System.Runtime.CompilerServices;
using System.Text.Json;
using UniversalDataDriven.Abstractions;
using UniversalDataDriven.Core;

/// <summary>
///     Maps <see cref="JsonValueKind.True" />/<see cref="JsonValueKind.False" /> to C# <c>bool</c>.
///     Non-widenable: a column that mixes bool with any other primitive is rejected at the schema level.
/// </summary>
[UddsgPlugin(typeof(IPrimitiveTypeRule))]
internal sealed class BoolPrimitiveRule : IPrimitiveTypeRule {
	[ModuleInitializer]
	internal static void Register() => UddsgPluginRegistry.Register(new BoolPrimitiveRule());

	public string Name => "bool";
	public int Rank => -1;
	public JsonValueKind BoundKind => JsonValueKind.True;

	public bool TryInfer(JsonValueModel value) =>
		value.Kind == JsonValueKind.True || value.Kind == JsonValueKind.False;

	public string EmitLiteral(JsonValueModel value) =>
		value.Kind == JsonValueKind.True ? "true" : value.Kind == JsonValueKind.False ? "false" : "false";

	public string EmitDefault() => "false";
}
