namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

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
    bool hasModdingPlugin,
    int loadMode,
    string schemaVersion,
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
    public bool HasModdingPlugin { get; } = hasModdingPlugin;
    public int LoadMode { get; } = loadMode;
    public string SchemaVersion { get; } = schemaVersion;
    public Location Location { get; } = location;
    public ITemplateMetadata? Template { get; } = template;
    public IReadOnlyDictionary<string, IReadOnlyList<RowData>>? RefToRows { get; set; }

    private readonly SourceProductionContext _spc = spc;

    public void ReportDiagnostic(Diagnostic diagnostic) => _spc.ReportDiagnostic(diagnostic);
    public System.Threading.CancellationToken CancellationToken => _spc.CancellationToken;
}
