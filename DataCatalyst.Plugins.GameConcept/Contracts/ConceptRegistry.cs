namespace DataCatalyst.Plugins.GameConcept;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Registry mapping concept tag types to concept names.
/// Optionally stores a Kind string for runtime filtering.
/// </summary>
public sealed class ConceptRegistry : IEnumerable<KeyValuePair<Type, string>> {
	/// <summary>Default instance for backward compatibility.</summary>
	public static readonly ConceptRegistry Default = new();

	private readonly Dictionary<string, Type> _nameToTag = [];
	private readonly Dictionary<Type, string> _tagToName = [];
	private readonly Dictionary<Type, string> _tagToKind = [];

	/// <summary>Register a concept: tag type to name, with optional kind.</summary>
	public void Register<TConcept>(string name, string? kind = null) where TConcept : struct {
		_nameToTag[name] = typeof(TConcept);
		_tagToName[typeof(TConcept)] = name;
		if (kind != null)
			_tagToKind[typeof(TConcept)] = kind;
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

	/// <summary>Resolve tag type to kind string.</summary>
	public string? ResolveKind<TConcept>() where TConcept : struct =>
		_tagToKind.TryGetValue(typeof(TConcept), out var kind) ? kind : null;

	/// <summary>Resolve tag type to kind string.</summary>
	public string? ResolveKind(Type tagType) =>
		_tagToKind.TryGetValue(tagType, out var kind) ? kind : null;

	/// <summary>All tag types registered with the given kind.</summary>
	public IReadOnlyCollection<Type> GetByKind(string kind) =>
		_tagToKind.Where(kv => kv.Value == kind).Select(kv => kv.Key).ToList();

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
