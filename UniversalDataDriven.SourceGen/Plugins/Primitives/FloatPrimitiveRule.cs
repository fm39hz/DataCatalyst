namespace UniversalDataDriven.Plugins.Primitives;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using UniversalDataDriven.Abstractions;
using UniversalDataDriven.Core;

/// <summary>
///     Maps a JSON non-integral (or out-of-long) number to C# <c>float</c>. Highest-rank widenable primitive
///     so it absorbs <see cref="IntPrimitiveRule" /> and <see cref="LongPrimitiveRule" /> in mixed columns.
/// </summary>
[UddsgPlugin(typeof(IPrimitiveTypeRule))]
internal sealed class FloatPrimitiveRule : IPrimitiveTypeRule {
	[ModuleInitializer]
	internal static void Register() => UddsgPluginRegistry.Register(new FloatPrimitiveRule());

	public string Name => "float";
	public int Rank => 3;
	public JsonValueKind BoundKind => JsonValueKind.Number;

	public bool TryInfer(JsonValueModel value) =>
		value.Kind == JsonValueKind.Number && !value.NumberIsIntegral;

	public string EmitLiteral(JsonValueModel value) {
		if (value.Kind != JsonValueKind.Number) {
			return "0f";
		}

		return value.NumberAsDouble.ToString("R", CultureInfo.InvariantCulture) + "f";
	}

	public string EmitDefault() => "0f";
}
