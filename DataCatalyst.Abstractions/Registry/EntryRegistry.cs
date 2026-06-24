using System;
using System.Collections.Generic;
using DataCatalyst.Storage;

namespace DataCatalyst.Registry;

public static class EntryRegistry
{
    public readonly record struct Record(Type EntryType, Type[] Concepts);

    private static readonly List<Record> _entries = new();
    private static readonly Dictionary<Type, Func<IStoragePool>> _poolFactories = new();
    private static Action<Type, int>? _indexAssigner;
    private static bool _frozen;

    public static void Register<TEntry>(params Type[] concepts)
        where TEntry : struct, IEntry
    {
        if (_frozen)
            throw new InvalidOperationException("Registry is frozen after pipeline build");
        _entries.Add(new(typeof(TEntry), concepts));
    }

    public static void RegisterPool(Type conceptType, Func<IStoragePool> factory)
    {
        if (_frozen)
            throw new InvalidOperationException("Registry is frozen after pipeline build");
        _poolFactories[conceptType] = factory;
    }

    public static IStoragePool CreatePool(Type conceptType)
    {
        if (_poolFactories.TryGetValue(conceptType, out var factory))
            return factory();
        return null!;
    }

    public static void RegisterIndexAssigner(Action<Type, int> assigner)
    {
        if (_frozen)
            throw new InvalidOperationException("Registry is frozen after pipeline build");
        _indexAssigner = assigner;
    }

    public static void AssignIndex(Type entryType, int index)
    {
        _indexAssigner?.Invoke(entryType, index);
    }

    public static IReadOnlyList<Record> All => _entries;
    public static int Count => _entries.Count;

    internal static void Freeze() => _frozen = true;
    internal static void Reset()
    {
        _entries.Clear();
        _poolFactories.Clear();
        _indexAssigner = null;
        _frozen = false;
    }
}
