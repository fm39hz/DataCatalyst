using System;
using System.Collections.Generic;

namespace DataCatalyst.Schema;

/// <summary>Central registry of concepts and aspects. Format-agnostic —
/// populated by any ISchemaLoader (JSON, YAML, TOML, etc.) or at runtime for mods.</summary>
public sealed class SchemaRegistry
{
    private readonly Dictionary<string, AspectSchema> _aspects
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _conceptAspects
        = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AspectSchema> Aspects => _aspects;
    public IReadOnlyDictionary<string, HashSet<string>> ConceptAspects => _conceptAspects;
    public bool IsReadOnly { get; private set; }

    /// <summary>Register an aspect type with its field schema.</summary>
    public void DefineAspect(string name, Dictionary<string, Type> fields)
    {
        if (IsReadOnly) throw new InvalidOperationException("Registry is frozen");
        _aspects[name] = new AspectSchema(name, fields);
    }

    /// <summary>Register a concept with its defining aspects.
    /// Aspects must already be defined.</summary>
    public void DefineConcept(string name, string[] aspectNames)
    {
        if (IsReadOnly) throw new InvalidOperationException("Registry is frozen");
        var set = new HashSet<string>(aspectNames, StringComparer.OrdinalIgnoreCase);
        foreach (var a in aspectNames)
            if (!_aspects.ContainsKey(a))
                throw new ArgumentException($"Aspect '{a}' not defined before concept '{name}'");
        _conceptAspects[name] = set;
    }

    /// <summary>Freeze — no further registrations allowed.</summary>
    public void Freeze() => IsReadOnly = true;

    public bool HasAspect(string name) => _aspects.ContainsKey(name);
    public bool HasConcept(string name) => _conceptAspects.ContainsKey(name);
    public AspectSchema? GetAspect(string name)
        => _aspects.TryGetValue(name, out var a) ? a : null;
    public HashSet<string>? GetConceptAspects(string concept)
        => _conceptAspects.TryGetValue(concept, out var a) ? a : null;
}
