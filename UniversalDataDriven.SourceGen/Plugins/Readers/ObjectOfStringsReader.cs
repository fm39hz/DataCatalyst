namespace UniversalDataDriven.Plugins.Readers;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using UniversalDataDriven.Abstractions;
using UniversalDataDriven.Core;

/// <summary>
///     Reads an object whose values are all <see cref="JsonValueKind.String" />. Emits one row per property,
///     with a single synthetic <c>value</c> column.
///     <para>
///         Example input:
///         <code>
///         {
///             "Idle":   "loop_idle",
///             "Attack": "play_attack"
///         }
///         </code>
///     </para>
///     <para>
///         <see cref="CanRead" /> is strict: the entry-point must be a non-empty object whose <em>every</em>
///         property is a string. Mixed objects (strings + numbers + nested) are rejected so the more general
///         <see cref="ObjectOfObjectsReader" /> can pick them up.
///     </para>
/// </summary>
[UddsgPlugin(typeof(IEntryPointReader))]
internal sealed class ObjectOfStringsReader : IEntryPointReader {
	[ModuleInitializer]
	internal static void Register() => UddsgPluginRegistry.Register(new ObjectOfStringsReader());

	public string Name => "ObjectOfStrings";

	public bool CanRead(JsonElement entryPoint, UddsgGenerationContext ctx) {
		if (entryPoint.ValueKind != JsonValueKind.Object) {
			return false;
		}

		var any = false;
		foreach (var prop in entryPoint.EnumerateObject()) {
			if (prop.Value.ValueKind != JsonValueKind.String) {
				return false;
			}

			any = true;
		}

		return any;
	}

	public IReadOnlyList<RowData>? Read(JsonElement entryPoint, UddsgGenerationContext ctx) {
		var rows = new List<RowData>();
		foreach (var prop in entryPoint.EnumerateObject()) {
			var key = prop.Name;
			if (!IdentifierGuard.IsValid(key)) {
				ctx.ReportDiagnostic(Diagnostic.Create(UddsgDiagnostics.InvalidIdentifier, ctx.Location, ctx.TargetFullyQualifiedName, key));
				return null;
			}

			var values = new Dictionary<string, JsonValueModel>(System.StringComparer.OrdinalIgnoreCase) {
				["value"] = JsonValueModel.From(prop.Value),
			};
			rows.Add(new RowData(key, values));
		}

		return rows;
	}
}
