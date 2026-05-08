namespace UniversalDataDriven.Plugins.Primitives;

using System.Runtime.CompilerServices;
using System.Text.Json;
using UniversalDataDriven.Abstractions;
using UniversalDataDriven.Core;

/// <summary>
///     Maps <see cref="JsonValueKind.String" /> to C# <c>string</c>. Non-widenable.
/// </summary>
[UddsgPlugin(typeof(IPrimitiveTypeRule))]
internal sealed class StringPrimitiveRule : IPrimitiveTypeRule {
	[ModuleInitializer]
	internal static void Register() => UddsgPluginRegistry.Register(new StringPrimitiveRule());

	public string Name => "string";
	public int Rank => -1;
	public JsonValueKind BoundKind => JsonValueKind.String;

	public bool TryInfer(JsonValueModel value) => value.Kind == JsonValueKind.String;

	public string EmitLiteral(JsonValueModel value) =>
		value.Kind == JsonValueKind.String ? CSharpStringLiteral(value.StringValue ?? string.Empty) : "string.Empty";

	public string EmitDefault() => "string.Empty";

	private static string CSharpStringLiteral(string s) =>
		"\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\0", "\\0") + "\"";
}
