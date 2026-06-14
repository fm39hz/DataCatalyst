namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Generic;

/// <summary>
///     Renders the final C# source string for a DataCatalyst target. The default emitter
///     (<c>PartialStructEmitter</c>) produces a partial type with an enum of keys, a static field per row,
///     and an <c>All</c> frozen dictionary.
///     <para>
///         Bespoke emitters can opt in by returning <c>true</c> from <see cref="Applies(DcGenerationContext)" />
///         only for specific contexts (e.g. when the target type is an enum, or when an attribute hint demands
///         a record-class layout). The driver picks the first matching emitter.
///     </para>
/// </summary>
public interface ITypeEmitter {
	public string Name { get; }

	public bool Applies(DcGenerationContext ctx);

	public string Emit(IReadOnlyList<RowData> rows, SchemaInfo schema, DcGenerationContext ctx);
}
