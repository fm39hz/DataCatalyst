namespace FM39hz.DataCatalyst.Test.Support;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;

/// <summary>
/// OCP proof: a third-party DataCatalyst primitive rule defined only in test assembly.
/// Registration happens via <c>[ModuleInitializer]</c> and must flow into registry automatically.
/// </summary>
public sealed class DecimalPrimitiveRule : IPrimitiveTypeRule {
	[ModuleInitializer]
	internal static void Register() => DcPluginRegistry.Register(new DecimalPrimitiveRule());

	public string Name => "decimal";
	public int Rank => 4;
	public JsonValueKind BoundKind => JsonValueKind.Number;
	public bool TryInfer(JsonValueModel value) => false;

	public string EmitLiteral(JsonValueModel value) =>
		value.Kind == JsonValueKind.Number
			? value.NumberAsDouble.ToString("R", CultureInfo.InvariantCulture) + "m"
			: "0m";

	public string EmitDefault() => "0m";
}
