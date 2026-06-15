namespace FM39hz.DataCatalyst.Abstractions;

public interface IScriptEngine {
    string Language { get; }
    IScriptContext CreateContext(string modId);
}
