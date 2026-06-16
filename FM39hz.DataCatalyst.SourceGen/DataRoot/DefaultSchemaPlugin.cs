namespace FM39hz.DataCatalyst.DataRoot;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

public sealed class DefaultSchemaPlugin : IDataRootPlugin {
    public string Name => "DefaultSchema";

    public bool CanHandle(string relativePath, string content)
        => true; // fallback — handles everything no other plugin claims

    public PluginResult Process(PluginContext ctx) {
        var scanner = new DataRootScanner();
        scanner.Scan(ctx.RootPrefix, new[] { (ctx.RelativePath, ctx.Content) });

        var graph = new InheritanceGraph();
        foreach (var s in scanner.Schemas) graph.AddSchema(s);
        foreach (var d in scanner.DataFiles) graph.AddNode(d);

        var emitter = new NativePocoEmitter(graph, ctx.Namespace);
        var code = emitter.EmitAll();
        if (code.Length == 0) return new PluginResult();

        return new PluginResult { SourceCode = code };
    }
}
