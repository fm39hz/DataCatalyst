namespace FM39hz.DataCatalyst.Plugins.Readers;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;
using Microsoft.CodeAnalysis;

/// <summary>
///     Reads the canonical <em>object-of-objects</em> shape: each top-level property name becomes a row key,
///     each property value (an object) becomes the row's columns.
///     <para>
///         Example input:
///         <code>
///         {
///             "Heavy": { "drag": 0.5, "mass": 100 },
///             "Light": { "drag": 0.1, "mass": 10  }
///         }
///         </code>
///     </para>
/// </summary>
[DcPlugin(typeof(IEntryPointReader))]
internal sealed class ObjectOfObjectsReader : IEntryPointReader {
	[ModuleInitializer]
	internal static void Register() => DcPluginRegistry.Register(new ObjectOfObjectsReader());

	public string Name => "ObjectOfObjects";

	public bool CanRead(JsonElement entryPoint, DcGenerationContext ctx) {
		if (entryPoint.ValueKind != JsonValueKind.Object) {
			return false;
		}

		// Must contain at least one property whose value is an object. An empty object would also match
		// vacuously here but produces zero rows downstream — the driver tolerates that case.
		foreach (var prop in entryPoint.EnumerateObject()) {
			if (prop.Value.ValueKind != JsonValueKind.Object) {
				return false;
			}
		}

		return true;
	}

	public IReadOnlyList<RowData>? Read(JsonElement entryPoint, DcGenerationContext ctx) {
		var rows = new List<RowData>();
		foreach (var prop in entryPoint.EnumerateObject()) {
			var key = prop.Name;
			if (!IdentifierGuard.IsValid(key)) {
				ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.InvalidIdentifier, ctx.Location, ctx.TargetFullyQualifiedName, key));
				return null;
			}

			if (prop.Value.ValueKind != JsonValueKind.Object) {
				ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.EntryPointShape, ctx.Location, ctx.TargetFullyQualifiedName, $"{ctx.EntryPointName}.{key}", prop.Value.ValueKind));
				return null;
			}

			rows.Add(new RowData(key, JsonValueModel.CloneObject(prop.Value)));
		}

		return rows;
	}
}
