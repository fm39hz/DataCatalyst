namespace FM39hz.DataCatalyst;

using Microsoft.CodeAnalysis;

internal static class DcDiagnostics {
	public static readonly DiagnosticDescriptor JsonNotFound = new("DC0001", "DataCatalyst: JSON file not found",
		"[CatalystData] on '{0}' references '{1}' which is not present in <AdditionalFiles>",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor JsonInvalid = new("DC0002", "DataCatalyst: Invalid JSON",
		"[CatalystData] on '{0}' could not parse '{1}': {2}",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor EntryPointMissing = new("DC0003", "DataCatalyst: Entry point missing",
		"[CatalystData] on '{0}' could not find entry point '{1}' in '{2}'",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor EntryPointShape = new("DC0004", "DataCatalyst: Entry point shape unsupported",
		"[CatalystData] on '{0}' expects entry '{1}' to be a JSON object or array-of-objects; got {2}",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor TargetNotPartial = new("DC0005", "DataCatalyst: Target type must be partial",
		"[CatalystData] target '{0}' must be declared as 'partial'",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor TemplateMissingMember = new("DC0006", "DataCatalyst: Template missing member",
		"[CatalystData] target '{0}' uses template '{1}' which has no member matching JSON key '{2}'",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Warning, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor InferenceUnsupported = new("DC0007", "DataCatalyst: Unsupported JSON shape for inference",
		"[CatalystData] on '{0}' encountered unsupported JSON shape at '{1}': {2}",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor InvalidIdentifier = new("DC0008", "DataCatalyst: JSON key is not a valid C# identifier",
		"[CatalystData] on '{0}' rejects key '{1}'",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor ArrayItemNotObject = new("DC0009", "DataCatalyst: Array entry-point items must be objects",
		"[CatalystData] on '{0}' expects every item in array entry '{1}' to be a JSON object; item at index {2} is {3}",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor ArrayKeyFieldRequired = new("DC0010", "DataCatalyst: Array entry-point requires KeyField",
		"[CatalystData] on '{0}' uses an array-of-objects entry-point ('{1}'); KeyField must be supplied",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor KindColumnCollision = new("DC0011", "DataCatalyst: Reserved member name 'Kind' collides with synthetic enum property",
		"[CatalystData] on '{0}' has a column named 'Kind' which collides with the synthetic enum property; rename the JSON column",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor NoReaderMatched = new("DC0012", "DataCatalyst: No entry-point reader matched",
		"[CatalystData] on '{0}' could not find a reader that accepts entry '{1}' (kind {2})",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor NoSchemaProviderMatched = new("DC0013", "DataCatalyst: No schema provider matched",
		"[CatalystData] on '{0}' could not find a schema provider",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor NoEmitterMatched = new("DC0014", "DataCatalyst: No type emitter matched",
		"[CatalystData] on '{0}' could not find a type emitter",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor SqliteFlatSchemaRequired = new("DC0015", "DataCatalyst: SQLite backend requires flat schema",
		"[CatalystData] on '{0}' uses Backend=Sqlite but has nested column '{1}'",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);

	public static readonly DiagnosticDescriptor JsonRuntimeFlatSchemaRequired = new("DC0016", "DataCatalyst: JSON runtime backend requires flat schema",
		"[CatalystData] on '{0}' uses Backend=Json but has nested column '{1}'",
		"FM39hz.DataCatalyst", DiagnosticSeverity.Error, isEnabledByDefault: true);
}
