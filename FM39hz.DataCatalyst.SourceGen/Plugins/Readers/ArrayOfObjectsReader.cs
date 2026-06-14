namespace FM39hz.DataCatalyst.Plugins.Readers;

using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;

/// <summary>
///     Reads an array of objects. Every item must be a JSON object; the row key is taken from the column
///     named <see cref="DcGenerationContext.KeyField" />.
///     <para>
///         <b>Breaking change vs. Wave 2:</b> auto-keying (<c>{TypeName}{Index}</c>) is deliberately not
///         supported here. Datasets must declare an explicit identifier column. This eliminates the silent
///         "Rule0", "Rule1" … class of names that hid the actual semantic intent of each row.
///     </para>
/// </summary>
[DcPlugin(typeof(IEntryPointReader))]
internal sealed class ArrayOfObjectsReader : IEntryPointReader {
	[ModuleInitializer]
	internal static void Register() => DcPluginRegistry.Register(new ArrayOfObjectsReader());

	public string Name => "ArrayOfObjects";

	public bool CanRead(JsonElement entryPoint, DcGenerationContext ctx) {
		if (entryPoint.ValueKind != JsonValueKind.Array) {
			return false;
		}

		// Empty array is a degenerate case the driver would accept (zero rows). Treat it as readable so
		// the user gets a generated empty registry rather than "no reader matched".
		foreach (var item in entryPoint.EnumerateArray()) {
			if (item.ValueKind != JsonValueKind.Object) {
				return false;
			}
		}

		return true;
	}

	public IReadOnlyList<RowData>? Read(JsonElement entryPoint, DcGenerationContext ctx) {
		if (string.IsNullOrEmpty(ctx.KeyField)) {
			ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.ArrayKeyFieldRequired, ctx.Location, ctx.TargetFullyQualifiedName, ctx.EntryPointName));
			return null;
		}

		var rows = new List<RowData>();
		var seenKeys = new HashSet<string>(System.StringComparer.Ordinal);
		var index = 0;
		foreach (var item in entryPoint.EnumerateArray()) {
			if (item.ValueKind != JsonValueKind.Object) {
				ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.ArrayItemNotObject, ctx.Location, ctx.TargetFullyQualifiedName, ctx.EntryPointName, index, item.ValueKind));
				return null;
			}

			var values = JsonValueModel.CloneObject(item);
			if (!values.TryGetValue(ctx.KeyField, out var keyModel) || keyModel.Kind != JsonValueKind.String) {
				ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.EntryPointShape, ctx.Location, ctx.TargetFullyQualifiedName, $"{ctx.EntryPointName}[{index}].{ctx.KeyField}", keyModel?.Kind ?? JsonValueKind.Undefined));
				return null;
			}

			var key = keyModel.StringValue ?? string.Empty;
			if (!IdentifierGuard.IsValid(key)) {
				ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.InvalidIdentifier, ctx.Location, ctx.TargetFullyQualifiedName, key));
				return null;
			}

			if (!seenKeys.Add(key)) {
				ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.InvalidIdentifier, ctx.Location, ctx.TargetFullyQualifiedName, key + " (duplicate at index " + index.ToString(CultureInfo.InvariantCulture) + ")"));
				return null;
			}

			rows.Add(new RowData(key, values));
			index++;
		}

		return rows;
	}
}
