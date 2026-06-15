namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System.Collections.Generic;

public interface IModServiceRegistry {
    void RegisterGlobal<T>(T service) where T : class;
    T? GetGlobal<T>() where T : class;
    void RegisterScoped<T>(string ownerModId, T service) where T : class;
    T? GetScoped<T>(string ownerModId) where T : class;
    IReadOnlyList<System.Type> AllServices { get; }
}
