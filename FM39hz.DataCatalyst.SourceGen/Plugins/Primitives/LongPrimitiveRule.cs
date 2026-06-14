namespace FM39hz.DataCatalyst.Plugins.Primitives;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;

/// <summary>
///     Maps a JSON integral number that overflows <see cref="int" /> to C# <c>long</c>. Widens to <c>float</c>.
/// </summary>
[DcPlugin(typeof(IPrimitiveTypeRule))]
internal sealed class LongPrimitiveRule : IPrimitiveTypeRule {
	[ModuleInitializer]
	internal static void Register() => DcPluginRegistry.Register(new LongPrimitiveRule());

	public string Name => "long";
	public int Rank => 2;
	public JsonValueKind BoundKind => JsonValueKind.Number;

	public bool TryInfer(JsonValueModel value) =>
		value.Kind == JsonValueKind.Number && value.NumberIsIntegral && !value.NumberFitsInt;

	public string EmitLiteral(JsonValueModel value) =>
		value.Kind == JsonValueKind.Number && value.NumberIsIntegral
			? value.NumberAsLong.ToString(CultureInfo.InvariantCulture) + "L"
			: "0L";

	public string EmitDefault() => "0L";
}
