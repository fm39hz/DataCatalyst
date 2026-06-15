namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

//
// HookDispatcher — called from IL-weaved game methods.
// Signatures are stable; Mono.Cecil emits calls to these exact patterns.
//
public static class HookDispatcher {
    public static bool Before(string methodId, object? instance, object?[] args, out object? returnValue) {
        var call = new HookCall(methodId, instance, args);
        if (HookRegistry.EmitBefore(methodId, ref call, out var skip)) {
            returnValue = call.ReturnValue;
            return skip;
        }
        returnValue = null;
        return false;
    }

    public static void After(string methodId, object? instance, object? returnValue) {
        var call = new HookCall(methodId, instance, ArrayEmpty<object?>());
        call.ReturnValue = returnValue;
        HookRegistry.EmitAfter(methodId, ref call);
    }

    private static T[] ArrayEmpty<T>() => System.Array.Empty<T>();
}
