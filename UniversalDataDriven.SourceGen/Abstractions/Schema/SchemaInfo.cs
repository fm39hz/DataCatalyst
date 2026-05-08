namespace UniversalDataDriven.Abstractions;

using System.Collections.Immutable;

/// <summary>
///     The full column set for every row of a UDDSG target. Columns are ordered: the order here is the order
///     in which fields appear inside the generated partial type.
/// </summary>
public sealed class SchemaInfo {
	public ImmutableArray<SchemaColumn> Columns { get; }

	public SchemaInfo(ImmutableArray<SchemaColumn> columns) {
		Columns = columns;
	}
}
