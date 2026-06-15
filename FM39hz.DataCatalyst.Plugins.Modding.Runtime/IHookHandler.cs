namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System;

public interface IHookHandler : IDisposable {
    bool? Before(string methodId, ref HookCall call);
    void After(string methodId, ref HookCall call);
}
