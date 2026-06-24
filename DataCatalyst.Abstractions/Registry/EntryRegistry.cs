using System;
using System.Collections.Generic;

namespace DataCatalyst.Registry;

public static class EntryRegistry
{
    public readonly record struct Record(Type EntryType, Type[] Concepts);

    private static readonly List<Record> _entries = new();
    private static bool _frozen;

    public static void Register<TEntry>(params Type[] concepts)
        where TEntry : struct, IEntry
    {
        if (_frozen)
            throw new InvalidOperationException("Registry is frozen after pipeline build");
        _entries.Add(new(typeof(TEntry), concepts));
    }

    public static IReadOnlyList<Record> All => _entries;
    public static int Count => _entries.Count;

    internal static void Freeze() => _frozen = true;
    internal static void Reset()
    {
        _entries.Clear();
        _frozen = false;
    }
}
