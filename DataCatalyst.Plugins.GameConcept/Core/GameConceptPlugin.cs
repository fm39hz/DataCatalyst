namespace DataCatalyst.Plugins.GameConcept;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DataCatalyst.Abstractions;
using DataCatalyst.Core;

/// <summary>
/// Plugin that builds concept-scoped catalogs after catalog resolution.
/// Self-contained: manages its own concept definitions, no core modifications.
/// Reads from ConceptRegistry.Default (auto-populated by SourceGen) and supports manual registration.
/// </summary>
[DataPlugin]
public sealed class GameConceptPlugin : ICatalogPlugin {
	private readonly Dictionary<string, HashSet<string>> _conceptEntries = [];
	private readonly Dictionary<Type, object> _conceptCatalogs = [];
	private DataCatalog? _catalog;

	/// <inheritdoc/>
	public bool IsEnabled => true;

	/// <inheritdoc/>
	public void OnLoad() { }

	/// <summary>The concept registry (defaults to ConceptRegistry.Default).</summary>
	public ConceptRegistry Registry => ConceptRegistry.Default;

	/// <summary>Register entries belonging to a concept.</summary>
	public void RegisterEntries<TConcept>(params string[] entryKeys) where TConcept : struct {
		var name = ConceptRegistry.Default.ResolveName<TConcept>()
			?? throw new InvalidOperationException(
				$"Concept '{typeof(TConcept).Name}' is not registered. Add [DataConcept] attribute.");

		if (!_conceptEntries.TryGetValue(name, out var set)) {
			set = [];
			_conceptEntries[name] = set;
		}
		foreach (var key in entryKeys) {
			set.Add(key);
		}
	}

	/// <summary>Load concept definitions from a JSON file.
	/// Format: { "Item": ["Sword", "Shield"], "Enemy": ["Goblin"] }</summary>
	public void LoadConcepts(string jsonPath) {
		if (!File.Exists(jsonPath)) {
			throw new FileNotFoundException($"Concepts file not found: {jsonPath}");
		}

		var text = File.ReadAllText(jsonPath);
		using var doc = JsonDocument.Parse(text);

		if (doc.RootElement.ValueKind != JsonValueKind.Object) {
			throw new InvalidOperationException(
				$"Concepts file must be a JSON object. Found: {doc.RootElement.ValueKind}");
		}

		foreach (var prop in doc.RootElement.EnumerateObject()) {
			var conceptName = prop.Name;
			if (prop.Value.ValueKind != JsonValueKind.Array) {
				continue;
			}

			if (!_conceptEntries.TryGetValue(conceptName, out var set)) {
				set = [];
				_conceptEntries[conceptName] = set;
			}

			foreach (var item in prop.Value.EnumerateArray()) {
				if (item.ValueKind == JsonValueKind.String) {
					var key = item.GetString();
					if (!string.IsNullOrEmpty(key)) {
						set.Add(key);
					}
				}
			}
		}
	}

	/// <summary>Gets a concept-scoped catalog by tag type.</summary>
	public ConceptCatalog<TConcept> GetConcept<TConcept>() where TConcept : struct {
		if (_conceptCatalogs.TryGetValue(typeof(TConcept), out var catalog) &&
			catalog is ConceptCatalog<TConcept> typed) {
			return typed;
		}
		throw new InvalidOperationException(
			$"Concept '{typeof(TConcept).Name}' not registered. " +
			"Call Registry.Register<TConcept>(name) and RegisterEntries() before catalog resolution.");
	}

	/// <summary>
	/// Called after catalog resolution. Builds concept-scoped catalogs
	/// from registered concept definitions and the resolved catalog.
	/// </summary>
	public void OnCatalogResolved(DataCatalog catalog, List<string> diagnostics) {
		_catalog = catalog;

		foreach (var kv in ConceptRegistry.Default) {
			var tagType = kv.Key;
			var name = kv.Value;
			if (!_conceptEntries.TryGetValue(name, out var entryKeys)) {
				diagnostics.Add(
					$"Concept '{name}' is registered but has no entries. " +
					"Call RegisterEntries() or LoadConcepts() before catalog resolution.");
				continue;
			}

			var filteredEntries = new Dictionary<string, DataEntry>();
			foreach (var key in entryKeys) {
				if (catalog.Entries.TryGetValue(key, out var entry)) {
					filteredEntries[key] = entry;
				}
				else {
					diagnostics.Add(
						$"Entry '{key}' for concept '{name}' not found in catalog.");
				}
			}

			if (filteredEntries.Count == 0) {
				diagnostics.Add(
					$"Concept '{name}' has no valid entries in catalog.");
				continue;
			}

			var conceptCatalog = CreateConceptCatalog(tagType, filteredEntries);
			if (conceptCatalog != null) {
				_conceptCatalogs[tagType] = conceptCatalog;
			}
		}
	}

	private object? CreateConceptCatalog(Type tagType, Dictionary<string, DataEntry> entries) {
		var catalogType = typeof(ConceptCatalog<>).MakeGenericType(tagType);
		var instance = Activator.CreateInstance(catalogType,
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
			null,
			[entries],
			null);

		if (instance != null) {
			var nameProp = catalogType.GetProperty(nameof(ConceptCatalog<int>.ConceptName));
			if (nameProp != null && ConceptRegistry.Default.ResolveName(tagType) is { } conceptName) {
				nameProp.SetValue(instance, conceptName);
			}
		}

		return instance;
	}
}
