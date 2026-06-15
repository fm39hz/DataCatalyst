namespace FM39hz.DataCatalyst.Plugins.Schema;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FM39hz.DataCatalyst.Abstractions;
using FM39hz.DataCatalyst.Core;
using Microsoft.CodeAnalysis;

/// <summary>
///     Walks every row's columns and infers a unified <see cref="SchemaInfo" /> by:
///     <list type="number">
///         <item>For each leaf value, asking every <see cref="IPrimitiveTypeRule" /> if it accepts the value.</item>
///         <item>Widening across rows by picking the rule with the highest <see cref="IPrimitiveTypeRule.Rank" />.</item>
///         <item>Recurring into arrays (homogeneous element type) and nested objects.</item>
///     </list>
///     <para>
///         When a column has mixed shapes (primitive vs array, or two non-widenable primitives), the
///         provider emits <see cref="DcDiagnostics.InferenceUnsupported" /> and aborts.
///     </para>
/// </summary>
[DcPlugin(typeof(ISchemaProvider))]
internal sealed class InferenceSchemaProvider : ISchemaProvider {
	[ModuleInitializer]
	internal static void Register() => DcPluginRegistry.Register(new InferenceSchemaProvider());

	public string Name => "Inference";

	public bool Applies(DcGenerationContext ctx) => ctx.Template is null;

	public SchemaInfo? Build(IReadOnlyList<RowData> rows, DcGenerationContext ctx) {
		if (rows.Count == 0) {
			return new SchemaInfo([]);
		}

		var columns = new Dictionary<string, SchemaColumn>(System.StringComparer.OrdinalIgnoreCase);
		var orderedNames = new List<string>();

		foreach (var row in rows) {
			foreach (var kv in row.Values) {
				if (!columns.TryGetValue(kv.Key, out var existing)) {
					orderedNames.Add(kv.Key);
					var inferred = InferTypeFromValue(kv.Value, ctx, kv.Key);
					if (inferred is null) {
						return null;
					}

					columns[kv.Key] = new SchemaColumn(kv.Key, inferred);
				}
				else {
					var widened = WidenType(existing.Type, kv.Value, ctx, kv.Key);
					if (widened is null) {
						return null;
					}

					columns[kv.Key] = new SchemaColumn(kv.Key, widened);
				}
			}
		}

		var ordered = ImmutableArray.CreateBuilder<SchemaColumn>(orderedNames.Count);
		foreach (var name in orderedNames) {
			ordered.Add(columns[name]);
		}

		return new SchemaInfo(ordered.ToImmutable());
	}

	private SchemaType? InferTypeFromValue(JsonValueModel v, DcGenerationContext ctx, string path) {
		switch (v.Kind) {
			case JsonValueKind.True:
			case JsonValueKind.False:
			case JsonValueKind.String:
			case JsonValueKind.Number:
				return InferPrimitive(v, ctx, path);

			case JsonValueKind.Array: {
					if (v.ArrayItems!.Count == 0) {
						ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.InferenceUnsupported, ctx.Location, ctx.TargetFullyQualifiedName, path, "empty array - element type cannot be inferred"));
						return null;
					}

					SchemaType? element = null;
					for (var i = 0; i < v.ArrayItems.Count; i++) {
						var inferred = InferTypeFromValue(v.ArrayItems[i], ctx, $"{path}[{i}]");
						if (inferred is null) {
							return null;
						}

						element = element is null ? inferred : WidenSchemaType(element, inferred, ctx, path);
						if (element is null) {
							return null;
						}
					}

					return SchemaType.OfArray(element!);
				}

			case JsonValueKind.Object: {
					var nestedRow = new Dictionary<string, JsonValueModel>(System.StringComparer.Ordinal);
					foreach (var kv in v.ObjectMembers!) {
						nestedRow[kv.Key] = kv.Value;
					}

					var nestedRows = new List<RowData> { new("__inline__", nestedRow) };
					var nested = Build(nestedRows, ctx);
					if (nested is null) {
						return null;
					}

					return SchemaType.OfObject(nested.Columns);
				}

			case JsonValueKind.Null:
				ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.InferenceUnsupported, ctx.Location, ctx.TargetFullyQualifiedName, path, "null leading value - type cannot be inferred"));
				return null;
			case JsonValueKind.Undefined:
			default:
				ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.InferenceUnsupported, ctx.Location, ctx.TargetFullyQualifiedName, path, $"unsupported JSON kind {v.Kind}"));
				return null;
		}
	}

	private static SchemaType? InferPrimitive(JsonValueModel v, DcGenerationContext ctx, string path) {
		// Walk primitive rules in deterministic dependency-topological order; the first whose TryInfer
		// accepts wins. For numbers this picks int (rank 1) before long (rank 2) before float (rank 3)
		// when the literal fits a narrower type, so widening - not inference - is what triggers float
		// adoption later on.
		foreach (var rule in DcPluginRegistry.Primitives) {
			if (rule.TryInfer(v)) {
				return SchemaType.OfPrimitive(rule.Name);
			}
		}

		ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.InferenceUnsupported, ctx.Location, ctx.TargetFullyQualifiedName, path, $"no IPrimitiveTypeRule accepts JSON kind {v.Kind}"));
		return null;
	}

	private SchemaType? WidenType(SchemaType existing, JsonValueModel v, DcGenerationContext ctx, string path) {
		if (v.Kind == JsonValueKind.Null) {
			return existing;
		}

		var inferred = InferTypeFromValue(v, ctx, path);
		if (inferred is null) {
			return null;
		}

		return WidenSchemaType(existing, inferred, ctx, path);
	}

	private SchemaType? WidenSchemaType(SchemaType a, SchemaType b, DcGenerationContext ctx, string path) {
		if (a.Equals(b)) {
			return a;
		}

		if (a.IsPrimitive && b.IsPrimitive) {
			var widened = WidenPrimitive(a.Primitive!, b.Primitive!);
			if (widened is null) {
				ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.InferenceUnsupported, ctx.Location, ctx.TargetFullyQualifiedName, path, $"incompatible primitives: {a.Primitive} vs {b.Primitive}"));
				return null;
			}

			return SchemaType.OfPrimitive(widened);
		}

		if (a.IsArray && b.IsArray) {
			var elem = WidenSchemaType(a.ArrayElement!, b.ArrayElement!, ctx, $"{path}[]");
			return elem is null ? null : SchemaType.OfArray(elem);
		}

		if (a.IsObject && b.IsObject) {
			var merged = new Dictionary<string, SchemaColumn>(System.StringComparer.OrdinalIgnoreCase);
			foreach (var c in a.ObjectColumns) {
				merged[c.Name] = c;
			}

			foreach (var c in b.ObjectColumns) {
				if (merged.TryGetValue(c.Name, out var existing)) {
					var widened = WidenSchemaType(existing.Type, c.Type, ctx, $"{path}.{c.Name}");
					if (widened is null) {
						return null;
					}

					merged[c.Name] = new SchemaColumn(c.Name, widened);
				}
				else {
					merged[c.Name] = c;
				}
			}

			var sorted = ImmutableArray.CreateBuilder<SchemaColumn>(merged.Count);
			var orderedKeys = new List<string>(merged.Keys);
			orderedKeys.Sort(System.StringComparer.Ordinal);
			foreach (var k in orderedKeys) {
				sorted.Add(merged[k]);
			}

			return SchemaType.OfObject(sorted.ToImmutable());
		}

		ctx.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.InferenceUnsupported, ctx.Location, ctx.TargetFullyQualifiedName, path, "rows disagree on column shape"));
		return null;
	}

	private static string? WidenPrimitive(string a, string b) {
		if (a == b) {
			return a;
		}

		IPrimitiveTypeRule? ra = null;
		IPrimitiveTypeRule? rb = null;
		foreach (var rule in DcPluginRegistry.Primitives) {
			if (rule.Name == a) {
				ra = rule;
			}

			if (rule.Name == b) {
				rb = rule;
			}
		}

		if (ra is null || rb is null) {
			return null;
		}

		// Non-widenable: at least one rank < 0 means we cannot reconcile two distinct named primitives.
		if (ra.Rank < 0 || rb.Rank < 0) {
			return null;
		}

		return ra.Rank >= rb.Rank ? a : b;
	}
}
