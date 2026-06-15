namespace FM39hz.DataCatalyst.Core;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using FM39hz.DataCatalyst.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>Orchestrates one generation run: resolves JSON, picks plugins (first-match + all companions), emits source.</summary>
internal static class PipelineDriver {
	internal static readonly Dictionary<string, IReadOnlyList<RowData>> CatalogRows = [];

	public static void Reset() => CatalogRows.Clear();

	public static void Run(SourceProductionContext spc, ImmutableArray<AdditionalText> additionalTexts, TargetInfo target) {
		if (!target.IsPartial) {
			spc.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.TargetNotPartial, target.Location, target.FullyQualifiedName));
			return;
		}

		var matched = FindAdditionalText(additionalTexts, target.JsonPath);
		if (matched is null) {
			spc.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.JsonNotFound, target.Location, target.FullyQualifiedName, target.JsonPath));
			return;
		}

		var rawText = matched.GetText(spc.CancellationToken)?.ToString();
		if (string.IsNullOrWhiteSpace(rawText)) {
			spc.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.JsonInvalid, target.Location, target.FullyQualifiedName, matched.Path, "empty file"));
			return;
		}

		JsonDocument doc;
		try {
			doc = JsonDocument.Parse(rawText!);
		}
		catch (JsonException ex) {
			spc.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.JsonInvalid, target.Location, target.FullyQualifiedName, matched.Path, ex.Message));
			return;
		}

		using (doc) {
			var entry = ResolveEntryPoint(doc.RootElement, target.EntryPoint);
			if (entry is null) {
				spc.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.EntryPointMissing, target.Location, target.FullyQualifiedName, target.EntryPoint, matched.Path));
				return;
			}

			var ctx = new DcGenerationContext(
				targetFullyQualifiedName: target.FullyQualifiedName,
				containingNamespace: target.ContainingNamespace,
				simpleName: target.SimpleName,
				typeKind: target.TypeKind,
				isRecord: target.IsRecord,
				entryPointName: target.EntryPoint,
				keyField: target.KeyField,
				jsonPath: target.JsonPath,
				backend: target.Backend,
				modSupport: target.ModSupport,
				location: target.Location,
				template: target.Template,
				spc: spc);

			IEntryPointReader? reader = null;
			foreach (var candidate in DcPluginRegistry.Readers) {
				if (candidate.CanRead(entry.Value, ctx)) {
					reader = candidate;
					break;
				}
			}

			if (reader is null) {
				spc.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.NoReaderMatched, target.Location, target.FullyQualifiedName, target.EntryPoint, entry.Value.ValueKind));
				return;
			}

			var rows = reader.Read(entry.Value, ctx);
			if (rows is null) {
				return;
			}

			CatalogRows[target.SimpleName] = rows;
			if (target.RefToTargets.Length > 0) {
				var refLookup = new Dictionary<string, IReadOnlyList<RowData>>();
				foreach (var rt in target.RefToTargets) {
					var dot = rt.LastIndexOf('.');
					var simple = dot >= 0 ? rt.Substring(dot + 1) : rt;
					if (CatalogRows.TryGetValue(simple, out var refData)) {
						refLookup[simple] = refData;
					}
				}
				ctx.RefToRows = refLookup;
			}

			ISchemaProvider? schemaProvider = null;
			foreach (var candidate in DcPluginRegistry.SchemaProviders) {
				if (candidate.Applies(ctx)) {
					schemaProvider = candidate;
					break;
				}
			}

			if (schemaProvider is null) {
				spc.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.NoSchemaProviderMatched, target.Location, target.FullyQualifiedName));
				return;
			}

			var schema = schemaProvider.Build(rows, ctx);
			if (schema is null) {
				return;
			}

			ITypeEmitter? emitter = null;
			foreach (var candidate in DcPluginRegistry.Emitters) {
				if (candidate.Applies(ctx)) {
					emitter = candidate;
					break;
				}
			}

			if (emitter is null) {
				spc.ReportDiagnostic(Diagnostic.Create(DcDiagnostics.NoEmitterMatched, target.Location, target.FullyQualifiedName));
				return;
			}

			var src = emitter.Emit(rows, schema, ctx);
			if (string.IsNullOrEmpty(src)) {
				return;
			}

			foreach (var post in DcPluginRegistry.PostProcessors) {
				post.After(src, ctx);
			}

			var hint = BuildHintName(target);
			spc.AddSource(hint, SourceText.From(src, Encoding.UTF8));

			foreach (var companion in DcPluginRegistry.CompanionEmitters) {
				if (!companion.Applies(ctx)) {
					continue;
				}

				var companionSrc = companion.Emit(rows, schema, ctx);
				if (string.IsNullOrEmpty(companionSrc)) {
					continue;
				}

				var companionHint = hint + "." + companion.Name;
				spc.AddSource(companionHint, SourceText.From(companionSrc, Encoding.UTF8));
			}
		}
	}

	private static AdditionalText? FindAdditionalText(ImmutableArray<AdditionalText> texts, string jsonPath) {
		var normalized = jsonPath.Replace('\\', '/').Trim();
		if (normalized.Length == 0) {
			return null;
		}

		foreach (var t in texts) {
			if (!t.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
				continue;
			}

			var p = t.Path.Replace('\\', '/');
			if (p.EndsWith(normalized, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(Path.GetFileName(p), normalized, StringComparison.OrdinalIgnoreCase)) {
				return t;
			}
		}
		return null;
	}

	private static JsonElement? ResolveEntryPoint(JsonElement root, string entryPoint) {
		if (string.IsNullOrEmpty(entryPoint)) {
			return root;
		}

		if (root.ValueKind != JsonValueKind.Object) {
			return null;
		}

		return root.TryGetProperty(entryPoint, out var ep) ? ep : null;
	}

	private static string BuildHintName(TargetInfo target) {
		var sanitized = target.FullyQualifiedName.Replace("global::", string.Empty).Replace('.', '_').Replace('<', '_').Replace('>', '_').Replace(' ', '_').Replace(',', '_');
		return sanitized + ".DataCatalyst.g.cs";
	}
}
