namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

/// <summary>
///     Read-only context passed to every plugin during a single DataCatalyst generation. Plugins use this to:
///     <list type="bullet">
///         <item>Identify the target type they are emitting for (<see cref="TargetFullyQualifiedName" />, <see cref="SimpleName" />).</item>
///         <item>Read the user-supplied attribute parameters (<see cref="EntryPointName" />, <see cref="KeyField" />).</item>
///         <item>Report Roslyn diagnostics through <see cref="ReportDiagnostic(Diagnostic)" />.</item>
///     </list>
///     <para>
///         The context intentionally does not expose a mutable bag - plugins must remain pure with respect to
///         each other. State that must flow between phases (e.g. inferred schema) is passed explicitly through
///         method arguments by the <c>PipelineDriver</c>.
///     </para>
/// </summary>
public sealed class DcGenerationContext(
	string targetFullyQualifiedName,
	string? containingNamespace,
	string simpleName,
	TypeKind typeKind,
	bool isRecord,
	string entryPointName,
	string keyField,
	string jsonPath,
	DataBackend backend,
	bool modSupport,
	Location location,
	ITemplateMetadata? template,
	SourceProductionContext spc) {
	public string TargetFullyQualifiedName { get; } = targetFullyQualifiedName;
	public string? ContainingNamespace { get; } = containingNamespace;
	public string SimpleName { get; } = simpleName;
	public TypeKind TypeKind { get; } = typeKind;
	public bool IsRecord { get; } = isRecord;
	public string EntryPointName { get; } = entryPointName;
	public string KeyField { get; } = keyField;
	public string JsonPath { get; } = jsonPath;
	public DataBackend Backend { get; } = backend;
	public bool ModSupport { get; } = modSupport;
	public Location Location { get; } = location;
	public ITemplateMetadata? Template { get; } = template;
	public IReadOnlyDictionary<string, IReadOnlyList<RowData>>? RefToRows { get; set; }

	private readonly SourceProductionContext _spc = spc;

	public void ReportDiagnostic(Diagnostic diagnostic) => _spc.ReportDiagnostic(diagnostic);

	public System.Threading.CancellationToken CancellationToken => _spc.CancellationToken;
}
