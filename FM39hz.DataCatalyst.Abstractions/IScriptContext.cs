namespace FM39hz.DataCatalyst.Abstractions;

using System;

public readonly struct ScriptError {
    public string ModId { get; }
    public string Message { get; }
    public Exception? Exception { get; }

    public ScriptError(string modId, string message, Exception? ex = null) {
        ModId = modId;
        Message = message;
        Exception = ex;
    }

    public override string ToString() => $"[{ModId}] {Message}";
}

public interface IScriptContext : IDisposable {
    string ModId { get; }
    ScriptError? LastError { get; }

    void SetGlobal(string name, object? value);
    void LoadFile(string path);
    bool TryCall(string functionName, out object? result, params object?[] args);
    event Action<ScriptError> OnError;
}
