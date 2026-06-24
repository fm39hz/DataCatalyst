using System;
using System.Collections.Generic;
using System.Linq;
using PipelineAbstractions = DataCatalyst.Pipeline;
using StageContext = DataCatalyst.Pipeline.PipelineContext;
using DataCatalyst.Storage;

namespace DataCatalyst.Stages;

internal sealed class ResolveCrossRefStage : PipelineAbstractions.IPipelineStage
{
    public string Id => "ResolveCrossRef";

    public void Execute(StageContext ctx)
    {
        var entries = ctx.Bag["RawEntries"] as List<RawEntry>;
        if (entries == null) return;

        var allKeys = ctx.Bag["AllKeys"] as HashSet<string>;
        if (allKeys == null) return;

        foreach (var entry in entries)
        {
            // 1. Walk and resolve "$ref" in raw fields
            foreach (var name in entry._rawFields.Keys.ToList())
            {
                var rawVal = entry._rawFields[name];
                bool modified = false;
                var resolvedVal = ResolveRefsInNode(rawVal, allKeys, ref modified, ctx, entry.Key, name);
                if (modified)
                {
                    entry._rawFields[name] = resolvedVal;
                }
            }

            // 2. Deserialize resolved raw C# object tree into entry.Components
            foreach (var name in entry._rawFields.Keys)
            {
                var rawVal = entry._rawFields[name];
                if (AspectTypeRegistry.TryGetType(name, out var aspectType))
                {
                    try
                    {
                        var d = AspectTypeRegistry.Deserialize(aspectType, rawVal);
                        if (d != null)
                        {
                            entry.Components[aspectType] = d;
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.Diagnostics.Error($"Failed to deserialize aspect '{name}' for entry '{entry.Key}': {ex.Message}");
                    }
                }
            }
        }
    }

    private static object? ResolveRefsInNode(object? node, HashSet<string> allKeys, ref bool modified, StageContext ctx, string entryKey, string aspectName)
    {
        if (node == null) return null;

        if (node is Dictionary<string, object?> obj)
        {
            // Check if this node is a reference object like {"$ref": "Target"}
            if (obj.TryGetValue("$ref", out var refVal) && refVal is string targetKey)
            {
                if (allKeys.Contains(targetKey))
                {
                    ctx.Diagnostics.Info($"Resolved cross-ref '{entryKey}.{aspectName}' → '{targetKey}'");
                }
                else
                {
                    ctx.Diagnostics.Warn($"Unresolved cross-ref '{entryKey}.{aspectName}' → '{targetKey}'");
                }
                modified = true;
                return targetKey; // replace the ref object with resolved targetKey string
            }

            // Recursively walk properties
            var keys = obj.Keys.ToList();
            foreach (var key in keys)
            {
                var val = obj[key];
                var resolved = ResolveRefsInNode(val, allKeys, ref modified, ctx, entryKey, aspectName + "." + key);
                if (resolved != val)
                {
                    obj[key] = resolved;
                    modified = true;
                }
            }
            return obj;
        }

        if (node is List<object?> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var val = list[i];
                var resolved = ResolveRefsInNode(val, allKeys, ref modified, ctx, entryKey, aspectName + $"[{i}]");
                if (resolved != val)
                {
                    list[i] = resolved;
                    modified = true;
                }
            }
            return list;
        }

        return node;
    }
}
