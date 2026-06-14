namespace FM39hz.DataCatalyst;

using Microsoft.CodeAnalysis;

/// <summary>
///     Diagnostics emitted by DataCatalyst.
///     IDs DC0001–DC0014 in category <c>FM39hz.DataCatalyst</c> are owned by this assembly.
/// </summary>
internal static class DcDiagnostics {
	public static readonly DiagnosticDescriptor JsonNotFound = new(
		id: "DC0001",
		title: "DataCatalyst: JSON file not found",
		messageFormat: "[CatalystData] on '{0}' references '{1}' which is not present in <AdditionalFiles>",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor JsonInvalid = new(
		id: "DC0002",
		title: "DataCatalyst: Invalid JSON",
		messageFormat: "[CatalystData] on '{0}' could not parse '{1}': {2}",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EntryPointMissing = new(
		id: "DC0003",
		title: "DataCatalyst: Entry point missing",
		messageFormat: "[CatalystData] on '{0}' could not find entry point '{1}' in '{2}'",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EntryPointShape = new(
		id: "DC0004",
		title: "DataCatalyst: Entry point shape unsupported",
		messageFormat: "[CatalystData] on '{0}' expects entry '{1}' to be a JSON object (object-of-objects, object-of-strings) or array-of-objects; got {2}",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor TargetNotPartial = new(
		id: "DC0005",
		title: "DataCatalyst: Target type must be partial",
		messageFormat: "[CatalystData] target '{0}' must be declared as 'partial' so the generator can extend it",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor TemplateMissingMember = new(
		id: "DC0006",
		title: "DataCatalyst: Template missing member",
		messageFormat: "[CatalystData] target '{0}' uses template '{1}' which has no member matching JSON key '{2}'",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor InferenceUnsupported = new(
		id: "DC0007",
		title: "DataCatalyst: Unsupported JSON shape for inference",
		messageFormat: "[CatalystData] on '{0}' encountered unsupported JSON shape at '{1}': {2}",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor InvalidIdentifier = new(
		id: "DC0008",
		title: "DataCatalyst: JSON key is not a valid C# identifier",
		messageFormat: "[CatalystData] on '{0}' rejects key '{1}' (must be a non-empty C# identifier; allowed: letters, digits, underscore; first char letter or underscore)",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ArrayItemNotObject = new(
		id: "DC0009",
		title: "DataCatalyst: Array entry-point items must be objects",
		messageFormat: "[CatalystData] on '{0}' expects every item in array entry '{1}' to be a JSON object; item at index {2} is {3}",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ArrayKeyFieldRequired = new(
		id: "DC0010",
		title: "DataCatalyst: Array entry-point requires KeyField",
		messageFormat: "[CatalystData] on '{0}' uses an array-of-objects entry-point ('{1}'); KeyField must be supplied so every row has a stable, declared C# identifier",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor KindColumnCollision = new(
		id: "DC0011",
		title: "DataCatalyst: Reserved member name 'Kind' collides with synthetic enum property",
		messageFormat: "[CatalystData] on '{0}' has a column named 'Kind' which collides with the synthetic enum property of the same name; rename the JSON column",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NoReaderMatched = new(
		id: "DC0012",
		title: "DataCatalyst: No entry-point reader matched the JSON shape",
		messageFormat: "[CatalystData] on '{0}' could not find a reader that accepts entry '{1}' (kind {2}); register a custom IEntryPointReader or restructure the JSON",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NoSchemaProviderMatched = new(
		id: "DC0013",
		title: "DataCatalyst: No schema provider matched the target",
		messageFormat: "[CatalystData] on '{0}' could not find a schema provider; register a custom ISchemaProvider",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NoEmitterMatched = new(
		id: "DC0014",
		title: "DataCatalyst: No type emitter matched the target",
		messageFormat: "[CatalystData] on '{0}' could not find a type emitter; register a custom ITypeEmitter",
		category: "FM39hz.DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);
}
