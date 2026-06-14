namespace FM39hz.DataCatalyst.Abstractions;

using Microsoft.CodeAnalysis;

/// <summary>
///     Read-only context passed to every plugin during a single DataCatalyst generation. Plugins use this to:
///     <list type="bullet">
///         <item>Identify the target type they are emitting for (<see cref="TargetFullyQualifiedName" />, <see cref="SimpleName" />).</item>
///         <item>Read the user-supplied attribute parameters (<see cref="EntryPointName" />, <see cref="KeyField" />).</item>
///         <item>Report Roslyn diagnostics through <see cref="ReportDiagnostic(Diagnostic)" />.</item>
///     </list>
///     <para>
///         The context intentionally does not expose a mutable bag — plugins must remain pure with respect to
///         each other. State that must flow between phases (e.g. inferred schema) is passed explicitly through
///         method arguments by the <c>PipelineDriver</c>.
///     </para>
/// </summary>
public sealed class DcGenerationContext {
	public string TargetFullyQualifiedName { get; }
	public string? ContainingNamespace { get; }
	public string SimpleName { get; }
	public TypeKind TypeKind { get; }
	public bool IsRecord { get; }
	public string EntryPointName { get; }
	public string KeyField { get; }
	public string JsonPath { get; }
	public Location Location { get; }
	public ITemplateMetadata? Template { get; }

	private readonly SourceProductionContext _spc;

	public DcGenerationContext(
		string targetFullyQualifiedName,
		string? containingNamespace,
		string simpleName,
		TypeKind typeKind,
		bool isRecord,
		string entryPointName,
		string keyField,
		string jsonPath,
		Location location,
		ITemplateMetadata? template,
		SourceProductionContext spc) {
		TargetFullyQualifiedName = targetFullyQualifiedName;
		ContainingNamespace = containingNamespace;
		SimpleName = simpleName;
		TypeKind = typeKind;
		IsRecord = isRecord;
		EntryPointName = entryPointName;
		KeyField = keyField;
		JsonPath = jsonPath;
		Location = location;
		Template = template;
		_spc = spc;
	}

	public void ReportDiagnostic(Diagnostic diagnostic) => _spc.ReportDiagnostic(diagnostic);

	public System.Threading.CancellationToken CancellationToken => _spc.CancellationToken;
}
