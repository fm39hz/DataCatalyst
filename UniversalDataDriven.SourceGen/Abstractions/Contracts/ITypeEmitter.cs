namespace UniversalDataDriven.Abstractions;

using System.Collections.Generic;

/// <summary>
///     Renders the final C# source string for a UDDSG target. The default emitter
///     (<c>PartialStructEmitter</c>) produces a partial type with an enum of keys, a static field per row,
///     and an <c>All</c> frozen dictionary.
///     <para>
///         Bespoke emitters can opt in by returning <c>true</c> from <see cref="Applies(UddsgGenerationContext)" />
///         only for specific contexts (e.g. when the target type is an enum, or when an attribute hint demands
///         a record-class layout). The driver picks the first matching emitter.
///     </para>
/// </summary>
public interface ITypeEmitter {
	string Name { get; }

	bool Applies(UddsgGenerationContext ctx);

	string Emit(IReadOnlyList<RowData> rows, SchemaInfo schema, UddsgGenerationContext ctx);
}
