namespace UniversalDataDriven.Test.Support;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using UniversalDataDriven.Abstractions;
using UniversalDataDriven.Core;

/// <summary>
/// OCP proof: a third-party UDDSG primitive rule defined only in test assembly.
/// Registration happens via <c>[ModuleInitializer]</c> and must flow into registry automatically.
/// </summary>
public sealed class DecimalPrimitiveRule : IPrimitiveTypeRule {
	[ModuleInitializer]
	internal static void Register() => UddsgPluginRegistry.Register(new DecimalPrimitiveRule());

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
