namespace UniversalDataDriven.Abstractions;

using System.Text.Json;

/// <summary>
///     A single primitive type understood by UDDSG (<c>int</c>, <c>long</c>, <c>float</c>, <c>bool</c>,
///     <c>string</c>, …). Each rule owns three concerns:
///     <list type="number">
///         <item>Inference: given a <see cref="JsonValueModel" />, decide whether the value belongs to this type.</item>
///         <item>Widening: when two rows disagree on a column, the rule with the higher <see cref="Rank" /> wins.</item>
///         <item>Emission: produce a C# literal (and a default literal) for the type.</item>
///     </list>
///     Adding a new primitive (e.g. <c>decimal</c>, <c>Guid</c>) is a single class — no edits to core or to
///     existing rules.
/// </summary>
public interface IPrimitiveTypeRule {
	/// <summary>The C# type name as it appears in emitted source (e.g. <c>"int"</c>, not <c>"System.Int32"</c>).</summary>
	string Name { get; }

	/// <summary>
	///     Numeric ranking used to pick a winner during widening. Higher rank "absorbs" lower rank.
	///     A negative rank marks a non-widenable type (e.g. <c>bool</c>, <c>string</c>): widening across
	///     two non-widenable types of different names is a hard error.
	/// </summary>
	int Rank { get; }

	/// <summary>
	///     Returns <c>true</c> if this rule is the canonical choice for <paramref name="value" />.
	///     For numbers the rule may consult <see cref="JsonValueModel.NumberIsIntegral" /> and
	///     <see cref="JsonValueModel.NumberFitsInt" /> to decide between <c>int</c>/<c>long</c>/<c>float</c>.
	/// </summary>
	bool TryInfer(JsonValueModel value);

	/// <summary>Returns the C# literal expression for the runtime value of <paramref name="value" />.</summary>
	string EmitLiteral(JsonValueModel value);

	/// <summary>Returns the C# literal expression for the type's logical default (e.g. <c>0</c>, <c>"\"\""</c>).</summary>
	string EmitDefault();

	/// <summary>
	///     The <see cref="JsonValueKind" /> this rule binds to. Used by the inference loop to short-circuit
	///     candidate enumeration.
	/// </summary>
	JsonValueKind BoundKind { get; }
}
