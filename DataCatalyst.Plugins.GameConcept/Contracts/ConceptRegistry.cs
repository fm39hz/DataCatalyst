namespace DataCatalyst.Plugins.ConceptDomain;

using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Registry mapping concept tag types to concept names.
/// Infrastructure provides this; consumer registers their domains.
/// </summary>
public sealed class ConceptRegistry : IEnumerable<KeyValuePair<Type, string>> {
	/// <summary>Default instance for backward compatibility.</summary>
	public static readonly ConceptRegistry Default = new();

	private readonly Dictionary<string, Type> _nameToTag = [];
	private readonly Dictionary<Type, string> _tagToName = [];

	/// <summary>Register a concept: tag type to name.</summary>
	public void Register<TConcept>(string name) where TConcept : struct {
		_nameToTag[name] = typeof(TConcept);
		_tagToName[typeof(TConcept)] = name;
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
