namespace UniversalDataDriven.Abstractions;

using System.Collections.Generic;
using System.Text.Json;

/// <summary>
///     Reads a JSON entry-point element and produces a flat list of rows.
///     <para>
///         A single UDDSG generation invokes exactly one reader. Selection is performed by
///         <c>PipelineDriver</c>, which iterates registered readers in registration order and picks the first
///         whose <see cref="CanRead(JsonElement, UddsgGenerationContext)" /> returns <c>true</c>.
///     </para>
///     <para>
///         <see cref="CanRead" /> implementations MUST be conservative: a reader returns <c>true</c> only
///         when it is structurally certain the entry-point matches its shape. Two readers must never both
///         return <c>true</c> for the same input — overlapping claims are a contract violation.
///     </para>
///     <para>
///         When <see cref="Read(JsonElement, UddsgGenerationContext)" /> returns <c>null</c> the reader has
///         already reported the relevant diagnostic and the driver aborts generation for this target.
///     </para>
/// </summary>
public interface IEntryPointReader {
	/// <summary>Stable identifier (e.g. <c>"ObjectOfObjects"</c>). Used in diagnostics and tests.</summary>
	string Name { get; }

	bool CanRead(JsonElement entryPoint, UddsgGenerationContext ctx);

	IReadOnlyList<RowData>? Read(JsonElement entryPoint, UddsgGenerationContext ctx);
}
