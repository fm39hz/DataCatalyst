namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System;
using System.Collections.Generic;
using FM39hz.DataCatalyst.Abstractions;

public static class EntryExposerRegistry {
    private static readonly List<Action<IScriptContext>> _exposers = new();
    private static readonly object _lock = new();

    public static void Register(Action<IScriptContext> exposer) {
        lock (_lock) {
            _exposers.Add(exposer);
        }
    }

    public static void ExposeAll(IScriptContext ctx) {
        Action<IScriptContext>[] snapshot;
        lock (_lock) {
            snapshot = _exposers.ToArray();
        }
        foreach (var exposer in snapshot) {
            exposer(ctx);
        }
    }
}
