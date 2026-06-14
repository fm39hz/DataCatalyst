namespace FM39hz.DataCatalyst.Plugins.Primitives;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;

/// <summary>
///     Maps a JSON integral number that fits in <see cref="int" /> to C# <c>int</c>. Widens to <c>long</c>
///     and <c>float</c> via <see cref="Rank" />.
/// </summary>
[DcPlugin(typeof(IPrimitiveTypeRule))]
internal sealed class IntPrimitiveRule : IPrimitiveTypeRule {
	[ModuleInitializer]
	internal static void Register() => DcPluginRegistry.Register(new IntPrimitiveRule());

	public string Name => "int";
	public int Rank => 1;
	public JsonValueKind BoundKind => JsonValueKind.Number;

	public bool TryInfer(JsonValueModel value) =>
		value.Kind == JsonValueKind.Number && value.NumberIsIntegral && value.NumberFitsInt;

	public string EmitLiteral(JsonValueModel value) =>
		value.Kind == JsonValueKind.Number && value.NumberIsIntegral
			? value.NumberAsLong.ToString(CultureInfo.InvariantCulture)
			: "0";

	public string EmitDefault() => "0";
}
