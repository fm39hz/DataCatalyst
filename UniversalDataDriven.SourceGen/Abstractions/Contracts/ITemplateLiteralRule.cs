namespace UniversalDataDriven.Abstractions;

/// <summary>
///     Emits literal/default expressions for template-bound non-primitive CLR types (for example enums).
///     This keeps the core emitter policy-free: it delegates concrete type literal syntax to pluggable rules.
/// </summary>
public interface ITemplateLiteralRule {
	/// <summary>Returns true when this rule can emit defaults for <paramref name="declaredType" />.</summary>
	bool CanEmitDefault(string declaredType);

	/// <summary>Returns true when this rule can emit <paramref name="declaredType" /> from <paramref name="value" />.</summary>
	bool CanEmit(string declaredType, JsonValueModel value);

	/// <summary>Returns a C# literal for <paramref name="declaredType" />.</summary>
	string EmitLiteral(string declaredType, JsonValueModel value);

	/// <summary>Returns a C# default expression for <paramref name="declaredType" />.</summary>
	string EmitDefault(string declaredType);
}
