namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System;

public struct HookCall {
    public string MethodId { get; }
    public object? Instance { get; }
    public object?[] Args { get; }
    public object? ReturnValue { get; set; }
    public Exception? Exception { get; set; }

    public HookCall(string methodId, object? instance, object?[] args) {
        MethodId = methodId;
        Instance = instance;
        Args = args;
        ReturnValue = null;
        Exception = null;
    }
}
