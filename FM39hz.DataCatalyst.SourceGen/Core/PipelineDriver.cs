namespace FM39hz.DataCatalyst.Core;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using FM39hz.DataCatalyst.Abstractions;

/// <summary>
///     Orchestrates one DataCatalyst generation per <see cref="TargetInfo" />. The driver is intentionally lean:
///     <list type="number">
///         <item>Resolve the JSON file from <c>AdditionalFiles</c>.</item>
///         <item>Parse + locate the entry-point.</item>
///         <item>Pick the first matching <see cref="IEntryPointReader" />.</item>
///         <item>Pick the first matching <see cref="ISchemaProvider" />.</item>
///         <item>Pick the first matching <see cref="ITypeEmitter" />.</item>
///         <item>Run every <see cref="IDcPostProcessor" /> against the emitted source.</item>
///         <item>Hand the source back to Roslyn via <see cref="SourceProductionContext.AddSource(string, SourceText)" />.</item>
///     </list>
///     <para>
///         Adding a new shape, primitive, schema source, or emission strategy requires zero edits here —
///         it is exclusively a matter of registering a new plugin. This is the OCP contract.
///     </para>
/// </summary>
internal static class PipelineDriver {
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
				// Emitter aborted (already reported a diagnostic).
				return;
			}

			foreach (var post in DcPluginRegistry.PostProcessors) {
				post.After(src, ctx);
			}

			var hint = BuildHintName(target);
			spc.AddSource(hint, SourceText.From(src, Encoding.UTF8));
		}
	}

	private static AdditionalText? FindAdditionalText(ImmutableArray<AdditionalText> texts, string jsonPath) {
		var normalized = jsonPath.Replace('\\', '/').Trim();
		if (normalized.Length == 0) {
			return null;
		}

		AdditionalText? best = null;
		foreach (var t in texts) {
			if (!t.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
				continue;
			}

			var p = t.Path.Replace('\\', '/');
			if (p.EndsWith(normalized, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(Path.GetFileName(p), normalized, StringComparison.OrdinalIgnoreCase)) {
				best = t;
				break;
			}
		}

		return best;
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
