namespace DataCatalyst.Schema;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SchemaRegistry {
	private readonly Dictionary<int, AspectSchema> _aspects = [];
	private readonly Dictionary<int, List<int>> _conceptAspects = [];
	private readonly Dictionary<string, int> _aspectNameToId = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _conceptNameToId = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<int, string> _aspectIdToName = [];
	private readonly Dictionary<int, string> _conceptIdToName = [];
	private int _nextAspectId, _nextConceptId;

	public IReadOnlyDictionary<int, AspectSchema> Aspects => _aspects;
	public IReadOnlyDictionary<int, List<int>> ConceptAspects => _conceptAspects;

	public int DefineAspect(string name, Dictionary<string, Type> fields) {
		if (_aspectNameToId.TryGetValue(name, out var existing)) {
			// Update fields if already defined (OntologyStage may register with object-first,
			// SchemaStage later provides correct types from generated code)
			_aspects[existing] = new AspectSchema(name, fields);
			return existing;
		}

		var id = _nextAspectId++;
		_aspects[id] = new AspectSchema(name, fields);
		_aspectNameToId[name] = id;
		_aspectIdToName[id] = name;
		return id;
	}

	public int DefineConcept(string name, string[] aspectNames) {
		if (!_conceptNameToId.TryGetValue(name, out var id)) {
			id = _nextConceptId++;
			_conceptNameToId[name] = id;
			_conceptIdToName[id] = name;
		}
		// Always update aspects: SchemaStage re-defines concepts with runtime
		// aspect mappings that may differ from ontology's initial definition.
		_conceptAspects[id] = [.. aspectNames.Select(GetAspectId).Where(x => x.HasValue).Select(x => x!.Value)];
		return id;
	}

	public int? GetAspectId(string name) =>
		_aspectNameToId.TryGetValue(name, out var id) ? id : null;
	public int? GetConceptId(string name) =>
		_conceptNameToId.TryGetValue(name, out var id) ? id : null;
	public bool TryGetAspectName(int id, out string? name) =>
		_aspectIdToName.TryGetValue(id, out name);
	public bool TryGetConceptName(int id, out string? name) =>
		_conceptIdToName.TryGetValue(id, out name);
	public bool HasAspect(int id) => _aspects.ContainsKey(id);
	public bool HasConcept(int id) => _conceptAspects.ContainsKey(id);
	public AspectSchema? GetAspect(int id) => _aspects.TryGetValue(id, out var a) ? a : null;
	public List<int>? GetConceptAspects(int conceptId) =>
		_conceptAspects.TryGetValue(conceptId, out var a) ? a : null;

	public void MergeFrom(SchemaRegistry other) {
		// Re-map other's aspects to local IDs by name
		foreach (var kv in other._aspectNameToId) {
			if (_aspectNameToId.ContainsKey(kv.Key)) {
				continue;
			}

			var newId = _nextAspectId++;
			_aspects[newId] = other._aspects[kv.Value];
			_aspectNameToId[kv.Key] = newId;
			_aspectIdToName[newId] = kv.Key;
		}
		foreach (var kv in other._conceptNameToId) {
			if (_conceptNameToId.ContainsKey(kv.Key)) {
				continue;
			}

			var newId = _nextConceptId++;
			_conceptNameToId[kv.Key] = newId;
			_conceptIdToName[newId] = kv.Key;
			var otherAspects = other._conceptAspects.GetValueOrDefault(kv.Value);
			if (otherAspects != null) {
				_conceptAspects[newId] = [.. otherAspects
					.Select(aid => other._aspectIdToName.TryGetValue(aid, out var an) && _aspectNameToId.TryGetValue(an, out var la) ? la : aid)];
			}
		}
	}
}
