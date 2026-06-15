namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System;

public sealed class FuncHookHandler : IHookHandler {
    private readonly Func<HookCall, bool?>? _before;
    private readonly Action<HookCall>? _after;

    public FuncHookHandler(Func<HookCall, bool?>? before = null, Action<HookCall>? after = null) {
        _before = before;
        _after = after;
    }

    public bool? Before(string methodId, ref HookCall call)
        => _before?.Invoke(call);

    public void After(string methodId, ref HookCall call)
        => _after?.Invoke(call);

    public void Dispose() { }
}
