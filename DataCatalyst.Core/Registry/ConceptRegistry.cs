namespace DataCatalyst.Core;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Abstractions;

/// <summary>
/// Registry mapping concept tag types to concept names.
/// Optionally stores a Kind marker Type for plugin-specific processing.
/// </summary>
public sealed class ConceptRegistry : IEnumerable<KeyValuePair<Type, string>> {
	/// <summary>Default instance for backward compatibility.</summary>
	public static readonly ConceptRegistry Default = new();

	private readonly Dictionary<string, Type> _nameToTag = [];
	private readonly Dictionary<Type, string> _tagToName = [];
	private readonly Dictionary<Type, Type> _tagToKind = [];
	private readonly Dictionary<Type, Func<IReadOnlyDictionary<int, DataEntry>, string, object>> _catalogFactories = [];

	/// <summary>Register a concept: tag type to name, with optional factory and kind marker type.</summary>
	public void Register<TConcept>(
		string name,
		Func<IReadOnlyDictionary<int, DataEntry>, string, object>? catalogFactory = null,
		Type? kind = null) where TConcept : struct {
		_nameToTag[name] = typeof(TConcept);
		_tagToName[typeof(TConcept)] = name;
		if (catalogFactory != null)
			_catalogFactories[typeof(TConcept)] = catalogFactory;
		if (kind != null)
			_tagToKind[typeof(TConcept)] = kind;
	}

	/// <summary>Creates a concept-scoped catalog using the registered factory.</summary>
	public object? CreateCatalog(Type tagType, IReadOnlyDictionary<int, DataEntry> entries, string conceptName) {
		return _catalogFactories.TryGetValue(tagType, out var factory) ? factory(entries, conceptName) : null;
	}

	/// <summary>Resolve concept name to tag type.</summary>
	public Type? ResolveType(string name) =>
		_nameToTag.TryGetValue(name, out var type) ? type : null;

	/// <summary>Resolve tag type to concept name.</summary>
	public string? ResolveName<TConcept>() where TConcept : struct =>
		_tagToName.TryGetValue(typeof(TConcept), out var name) ? name : null;

	/// <summary>Resolve tag type to concept name.</summary>
	public string? ResolveName(Type tagType) =>
		_tagToName.TryGetValue(tagType, out var name) ? name : null;

	/// <summary>Resolve tag type to kind marker type (null = unset).</summary>
	public Type? ResolveKind<TConcept>() where TConcept : struct =>
		_tagToKind.TryGetValue(typeof(TConcept), out var kind) ? kind : null;

	/// <summary>Resolve tag type to kind marker type (null = unset).</summary>
	public Type? ResolveKind(Type tagType) =>
		_tagToKind.TryGetValue(tagType, out var kind) ? kind : null;

	/// <summary>All tag types registered with the given kind marker type.</summary>
	public IReadOnlyCollection<Type> GetByKind<TKind>() where TKind : struct =>
		_tagToKind.Where(kv => kv.Value == typeof(TKind)).Select(kv => kv.Key).ToList();

	/// <summary>All tag types registered with the given kind marker type.</summary>
	public IReadOnlyCollection<Type> GetByKind(Type kindType) =>
		_tagToKind.Where(kv => kv.Value == kindType).Select(kv => kv.Key).ToList();

	/// <summary>Check if concept is registered.</summary>
	public bool IsRegistered<TConcept>() where TConcept : struct =>
		_tagToName.ContainsKey(typeof(TConcept));

	/// <summary>All registered concept names.</summary>
	public IReadOnlyCollection<string> ConceptNames => _nameToTag.Keys;

	/// <summary>Number of registered concepts.</summary>
	public int Count => _nameToTag.Count;

	/// <inheritdoc/>
	public IEnumerator<KeyValuePair<Type, string>> GetEnumerator() =>
		_tagToName.GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
