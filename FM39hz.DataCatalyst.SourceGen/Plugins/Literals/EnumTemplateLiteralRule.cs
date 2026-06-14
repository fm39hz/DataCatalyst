namespace FM39hz.DataCatalyst.Plugins.Literals;

using System.Runtime.CompilerServices;
using System.Text.Json;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;

[DcPlugin(typeof(ITemplateLiteralRule))]
internal sealed class EnumTemplateLiteralRule : ITemplateLiteralRule {
	[ModuleInitializer]
	internal static void Register() => DcPluginRegistry.Register(new EnumTemplateLiteralRule());

	public bool CanEmitDefault(string declaredType) => LooksLikeQualifiedEnumType(declaredType);

	public bool CanEmit(string declaredType, JsonValueModel value) {
		if (value.Kind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.StringValue)) {
			return false;
		}

		if (!LooksLikeQualifiedEnumType(declaredType)) {
			return false;
		}

		return IdentifierGuard.IsValid(value.StringValue!);
	}

	public string EmitLiteral(string declaredType, JsonValueModel value) =>
		declaredType + "." + value.StringValue;

	public string EmitDefault(string declaredType) => "default(" + declaredType + ")";

	private static bool LooksLikeQualifiedEnumType(string declaredType) {
		if (string.IsNullOrWhiteSpace(declaredType) || !declaredType.StartsWith("global::", System.StringComparison.Ordinal)) {
			return false;
		}

		var typeName = declaredType.Substring("global::".Length);
		if (typeName.Length == 0) {
			return false;
		}

		var segments = typeName.Split('.');
		foreach (var segment in segments) {
			if (!IdentifierGuard.IsValid(segment)) {
				return false;
			}
		}

		return true;
	}
}
