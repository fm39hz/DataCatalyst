using System;
using DataCatalyst.Storage;
using System.Collections.Generic;
using System.Linq;
using PipelineAbstractions = DataCatalyst.Pipeline;
using StageContext = DataCatalyst.Pipeline.PipelineContext;

using DataCatalyst.Registry;
using WorldAbstractions = DataCatalyst.World;
using DataCatalyst;

namespace DataCatalyst.Stages;

internal sealed class BuildWorldStage : PipelineAbstractions.IPipelineStage
{
    public string Id => "BuildWorld";

    public void Execute(StageContext ctx)
    {
        DataCatalyst.Storage.AspectTypeRegistry.Initialize();
        var entries = ctx.Bag["RawEntries"] as List<RawEntry>;
        if (entries == null)
        {
            ctx.Diagnostics.Error("No entries to build world");
            return;
        }

        // Group entries by concept
        var byConcept = new Dictionary<string, List<RawEntry>>();
        foreach (var entry in entries)
        {
            foreach (var concept in entry.Concepts)
            {
                if (!byConcept.ContainsKey(concept))
                    byConcept[concept] = new List<RawEntry>();
                byConcept[concept].Add(entry);
            }
        }

        // Build pools per concept
        var pools = new Dictionary<Type, DataCatalyst.Storage.IStoragePool>();

        foreach (var kv in byConcept)
        {
            var conceptName = kv.Key;
            var conceptEntries = kv.Value;

            // Try to match concept name to a registered concept type
            Type? conceptType = null;
            foreach (var record in EntryRegistry.All)
            {
                foreach (var c in record.Concepts)
                {
                    if (c.Name == conceptName)
                    {
                        conceptType = c;
                        break;
                    }
                }
                if (conceptType != null) break;
            }

            // For now, skip concepts without registered type (no SourceGen yet)
            if (conceptType == null)
            {
                ctx.Diagnostics.Warn($"Concept '{conceptName}' has no registered type — skipping pool creation");
                continue;
            }

            // Find max index for this concept
            var conceptEntryIds = new HashSet<string>(conceptEntries.Select(e => e.Key));
            int maxIndex = conceptEntries.Max(e => e.AssignedIndex);
            if (maxIndex < 0) continue;

            // Create generic pool (temporary until SourceGen generates typed pools)
            var pool = new GenericPool(conceptEntries);
            pool.Resize(maxIndex + 1);

            foreach (var entry in conceptEntries)
            {
                pool.PopulateFrom(entry.AssignedIndex, entry);
            }

            pools[conceptType] = pool;

            // Assign EntryIndex for entries of this concept
            foreach (var record in EntryRegistry.All)
            {
                bool belongsToThisConcept = false;
                foreach (var c in record.Concepts)
                {
                    if (c == conceptType) { belongsToThisConcept = true; break; }
                }
                if (!belongsToThisConcept) continue;

                var entryName = record.EntryType.Name;
                var foundEntry = entries.FirstOrDefault(e => e.Key == entryName);
                if (foundEntry != null)
                {
                    var idxProp = typeof(EntryIndex<>)
                        .MakeGenericType(record.EntryType)
                        .GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (idxProp != null)
                        idxProp.SetValue(null, foundEntry.AssignedIndex);
                }
            }
        }

        // Build World
        ctx.World = DataCatalyst.World.WorldFactory.Create(pools);
        ctx.Diagnostics.Info($"Built world with {pools.Count} concept pools");
    }
}

// Temporary generic pool until SourceGen generates typed pools
internal sealed class GenericPool : DataCatalyst.Storage.IStoragePool
{
    private readonly List<Dictionary<Type, object>> _rows = new();
    private readonly Dictionary<int, Dictionary<string, string>> _rawFields = new();
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true
    };

    public GenericPool(List<RawEntry> sourceEntries)
    {
        System.Console.Error.WriteLine($"[TRACE] GenericPool: {sourceEntries.Count} entries");
        foreach (var entry in sourceEntries)
        {
            System.Console.Error.WriteLine($"[TRACE]   entry '{entry.Key}' idx={entry.AssignedIndex} rawFields={entry._rawFields.Count} comps={entry.Components.Count}");
            if (entry._rawFields.Count > 0)
                _rawFields[entry.AssignedIndex] = new(entry._rawFields, StringComparer.OrdinalIgnoreCase);
        }
    }

    public int Count => _rows.Count;

    public void Resize(int size)
    {
        while (_rows.Count < size)
            _rows.Add(new Dictionary<Type, object>());
    }

    public void PopulateFrom(int index, RawEntry entry)
    {
        if (index < 0 || index >= _rows.Count)
            return;
        var row = _rows[index];
        foreach (var kv in entry.Components)
            row[kv.Key] = kv.Value;
    }

    public T Get<T>(int index) where T : struct
    {
        if (index < 0 || index >= _rows.Count)
            throw new IndexOutOfRangeException();

        // Try typed lookup first
        if (_rows[index].TryGetValue(typeof(T), out var val))
        {
            System.Console.Error.WriteLine($"[TRACE] Get<{typeof(T).Name}>({index}) → typed hit");
            return (T)val;
        }

        // Fallback: deserialize from raw JSON by type name
        var typeName = typeof(T).Name;
        System.Console.Error.WriteLine($"[TRACE] Get<{typeName}>({index}) → typed miss, rawFields={_rawFields.Count}");
        if (_rawFields.TryGetValue(index, out var fields))
        {
            System.Console.Error.WriteLine($"[TRACE]   rawFields keys: {string.Join(",", fields.Keys)}");
            if (fields.TryGetValue(typeName, out var rawJson))
            {
                try
                {
                    var result = System.Text.Json.JsonSerializer.Deserialize<T>(rawJson, _jsonOpts);
                    _rows[index][typeof(T)] = result;
                    System.Console.Error.WriteLine($"[TRACE]   deserialized from raw JSON OK");
                    return result;
                }
                catch (Exception ex)
                {
                    System.Console.Error.WriteLine($"[TRACE]   deserialize error: {ex.Message}");
                }
            }
        }
        else
        {
            System.Console.Error.WriteLine($"[TRACE]   no raw fields for index {index}");
        }

        return default;
    }

    public void Set<T>(int index, T value) where T : struct
    {
        if (index < 0 || index >= _rows.Count)
            throw new IndexOutOfRangeException();
        _rows[index][typeof(T)] = value;
    }
}
