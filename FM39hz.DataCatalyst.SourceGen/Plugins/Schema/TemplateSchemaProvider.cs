namespace FM39hz.DataCatalyst.Plugins.Schema;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;
using Microsoft.CodeAnalysis;

/// <summary>
///     Builds the schema directly from the user-supplied <c>TemplateType</c>. Every public property/field of
///     the template becomes a column with the corresponding fully-qualified C# type. JSON columns that have
///     no template member raise <see cref="DcDiagnostics.TemplateMissingMember" /> as a warning so users
///     notice typos without breaking the build.
/// </summary>
[DcPlugin(typeof(ISchemaProvider))]
internal sealed class TemplateSchemaProvider : ISchemaProvider {
	[ModuleInitializer]
	internal static void Register() => DcPluginRegistry.Register(new TemplateSchemaProvider());

	public string Name => "Template";

	public bool Applies(DcGenerationContext ctx) => ctx.Template is not null;

	public SchemaInfo? Build(IReadOnlyList<RowData> rows, DcGenerationContext ctx) {
		var template = ctx.Template!;
		var byName = new Dictionary<string, TemplateMember>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var m in template.Members) {
			byName[m.Name] = m;
		}

		foreach (var row in rows) {
			foreach (var kv in row.Values) {
				if (!byName.ContainsKey(kv.Key)) {
					ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.TemplateMissingMember, ctx.Location, ctx.TargetFullyQualifiedName, template.FullyQualifiedName, kv.Key));
				}
			}
		}

		var cols = ImmutableArray.CreateBuilder<SchemaColumn>(template.Members.Length);
		foreach (var m in template.Members) {
			cols.Add(new SchemaColumn(m.Name, ParseTemplateType(m.TypeFullyQualified)));
		}

		return new SchemaInfo(cols.ToImmutable());
	}

	private static SchemaType ParseTemplateType(string fullyQualifiedType) {
		if (fullyQualifiedType.EndsWith("[]", System.StringComparison.Ordinal)) {
			var elementType = fullyQualifiedType.Substring(0, fullyQualifiedType.Length - 2);
			return SchemaType.OfArray(SchemaType.PrimitiveLiteral(elementType));
		}

		const string immutableArrayPrefix = "global::System.Collections.Immutable.ImmutableArray<";
		if (fullyQualifiedType.StartsWith(immutableArrayPrefix, System.StringComparison.Ordinal)
			&& fullyQualifiedType.EndsWith(">", System.StringComparison.Ordinal)) {
			var elementType = fullyQualifiedType.Substring(immutableArrayPrefix.Length, fullyQualifiedType.Length - immutableArrayPrefix.Length - 1);
			return SchemaType.OfArray(SchemaType.PrimitiveLiteral(elementType));
		}

		return SchemaType.PrimitiveLiteral(fullyQualifiedType);
	}
}
