namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System;
using System.Collections.Generic;

public static class HookRegistry {
    private static readonly Dictionary<string, List<WeakReference<IHookHandler>>> _handlers = new();
    private static readonly object _lock = new();

    public static IDisposable Register(string methodId, IHookHandler handler) {
        lock (_lock) {
            if (!_handlers.TryGetValue(methodId, out var list))
                _handlers[methodId] = list = new();
            list.Add(new WeakReference<IHookHandler>(handler));
        }
        return handler;
    }

    public static IDisposable Register(string methodId,
        Func<HookCall, bool?>? before = null,
        Action<HookCall>? after = null) {
        return Register(methodId, new FuncHookHandler(before, after));
    }

    public static void UnregisterAll() {
        lock (_lock) _handlers.Clear();
    }

    internal static bool EmitBefore(string methodId, ref HookCall call, out bool skip) {
        skip = false;
        var list = GetHandlers(methodId);
        if (list is null) return false;

        for (var i = list.Count - 1; i >= 0; i--) {
            var h = list[i];
            if (h is null) { list.RemoveAt(i); continue; }
            try {
                var result = h.Before(methodId, ref call);
                if (result == true) skip = true;
            } catch {
                list.RemoveAt(i); // dead handler
            }
        }
        return true;
    }

    internal static bool EmitAfter(string methodId, ref HookCall call) {
        var list = GetHandlers(methodId);
        if (list is null) return false;

        for (var i = list.Count - 1; i >= 0; i--) {
            var h = list[i];
            if (h is null) { list.RemoveAt(i); continue; }
            try {
                h.After(methodId, ref call);
            } catch {
                list.RemoveAt(i);
            }
        }
        return true;
    }

    private static List<IHookHandler?>? GetHandlers(string methodId) {
        List<WeakReference<IHookHandler>>? wrList;
        lock (_lock) {
            if (!_handlers.TryGetValue(methodId, out wrList))
                return null;
        }

        var live = new List<IHookHandler?>(wrList.Count);
        for (var i = 0; i < wrList.Count; i++) {
            if (wrList[i].TryGetTarget(out var h))
                live.Add(h);
        }
        return live;
    }
}
