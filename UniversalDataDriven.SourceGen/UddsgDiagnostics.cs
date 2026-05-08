namespace UniversalDataDriven;

using Microsoft.CodeAnalysis;

/// <summary>
///     Diagnostics emitted by the Universal Data-Driven Source Generator (UDDSG).
///     IDs SAMSARA020 and above in category <c>Samsara.Uddsg</c> are owned by this assembly (through SAMSARA033 as of Wave 3).
/// </summary>
internal static class UddsgDiagnostics {
	public static readonly DiagnosticDescriptor JsonNotFound = new(
		id: "SAMSARA020",
		title: "UDDSG: JSON file not found",
		messageFormat: "[GenerateFromData] on '{0}' references '{1}' which is not present in <AdditionalFiles>",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor JsonInvalid = new(
		id: "SAMSARA021",
		title: "UDDSG: Invalid JSON",
		messageFormat: "[GenerateFromData] on '{0}' could not parse '{1}': {2}",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EntryPointMissing = new(
		id: "SAMSARA022",
		title: "UDDSG: Entry point missing",
		messageFormat: "[GenerateFromData] on '{0}' could not find entry point '{1}' in '{2}'",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EntryPointShape = new(
		id: "SAMSARA023",
		title: "UDDSG: Entry point shape unsupported",
		messageFormat: "[GenerateFromData] on '{0}' expects entry '{1}' to be a JSON object (object-of-objects, object-of-strings) or array-of-objects; got {2}",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor TargetNotPartial = new(
		id: "SAMSARA024",
		title: "UDDSG: Target type must be partial",
		messageFormat: "[GenerateFromData] target '{0}' must be declared as 'partial' so the generator can extend it",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor TemplateMissingMember = new(
		id: "SAMSARA025",
		title: "UDDSG: Template missing member",
		messageFormat: "[GenerateFromData] target '{0}' uses template '{1}' which has no member matching JSON key '{2}'",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor InferenceUnsupported = new(
		id: "SAMSARA026",
		title: "UDDSG: Unsupported JSON shape for inference",
		messageFormat: "[GenerateFromData] on '{0}' encountered unsupported JSON shape at '{1}': {2}",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor InvalidIdentifier = new(
		id: "SAMSARA027",
		title: "UDDSG: JSON key is not a valid C# identifier",
		messageFormat: "[GenerateFromData] on '{0}' rejects key '{1}' (must be a non-empty C# identifier; allowed: letters, digits, underscore; first char letter or underscore)",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ArrayItemNotObject = new(
		id: "SAMSARA028",
		title: "UDDSG: Array entry-point items must be objects",
		messageFormat: "[GenerateFromData] on '{0}' expects every item in array entry '{1}' to be a JSON object; item at index {2} is {3}",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ArrayKeyFieldRequired = new(
		id: "SAMSARA029",
		title: "UDDSG: Array entry-point requires KeyField",
		messageFormat: "[GenerateFromData] on '{0}' uses an array-of-objects entry-point ('{1}'); KeyField must be supplied so every row has a stable, declared C# identifier",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor KindColumnCollision = new(
		id: "SAMSARA030",
		title: "UDDSG: Reserved member name 'Kind' collides with synthetic enum property",
		messageFormat: "[GenerateFromData] on '{0}' has a column named 'Kind' which collides with the synthetic enum property of the same name; rename the JSON column",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NoReaderMatched = new(
		id: "SAMSARA031",
		title: "UDDSG: No entry-point reader matched the JSON shape",
		messageFormat: "[GenerateFromData] on '{0}' could not find a reader that accepts entry '{1}' (kind {2}); register a custom IEntryPointReader or restructure the JSON",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NoSchemaProviderMatched = new(
		id: "SAMSARA032",
		title: "UDDSG: No schema provider matched the target",
		messageFormat: "[GenerateFromData] on '{0}' could not find a schema provider; register a custom ISchemaProvider",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NoEmitterMatched = new(
		id: "SAMSARA033",
		title: "UDDSG: No type emitter matched the target",
		messageFormat: "[GenerateFromData] on '{0}' could not find a type emitter; register a custom ITypeEmitter",
		category: "Samsara.Uddsg",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);
}
