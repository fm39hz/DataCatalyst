namespace FM39hz.DataCatalyst.Abstractions;

using System;

public struct HookArgs {
    public string MethodId { get; set; }
    public object? Instance { get; set; }
    public object?[] Args { get; set; }
    public object? ReturnValue { get; set; }
    public Exception? Exception { get; set; }
}
