using FM39hz.DataCatalyst.Runtime;
using MoonSharp.Interpreter;

public sealed class ScriptBridge {
    private readonly Script _lua = new();

    public ScriptBridge() {
        // Generated bridges — one line per catalog, no manual wrapping
        ItemLua.Register(_lua);
        BuffLua.Register(_lua);

        _lua.Globals["ECS"] = new EcsBridge(store, root);
    }

    public void LoadModScripts(string dir) {
        foreach (var file in Directory.EnumerateFiles(dir, "*.lua"))
            _lua.DoFile(file);
    }
}
