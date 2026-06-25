using System;
using System.Collections.Generic;
using DataCatalyst.Storage;

namespace DataCatalyst.Registry;

/// <summary>Global registry of entry types and their pool factories.
/// Populated by SourceGen-generated ModuleInitializer code at assembly load.
/// Frozen after pipeline build to prevent mutation.</summary>
public static class EntryRegistry
{
    public readonly record struct Record(Type EntryType, Type[] Concepts);

    private static readonly List<Record> _entries = new();
    private static readonly Dictionary<Type, Func<IStoragePool>> _poolFactories = new();
    private static bool _frozen;

    public static void Register<TEntry>(params Type[] concepts)
        where TEntry : struct, IEntry
    {
        if (_frozen) throw new InvalidOperationException("Registry frozen after pipeline build");
        _entries.Add(new(typeof(TEntry), concepts));
    }

    public static void RegisterPool(Type conceptType, Func<IStoragePool> factory)
    {
        if (_frozen) throw new InvalidOperationException("Registry frozen after pipeline build");
        _poolFactories[conceptType] = factory;
    }

    public static IStoragePool? CreatePool(Type conceptType)
        => _poolFactories.TryGetValue(conceptType, out var f) ? f() : null;

    public static IReadOnlyList<Record> All => _entries;

    internal static void Freeze() => _frozen = true;
}
