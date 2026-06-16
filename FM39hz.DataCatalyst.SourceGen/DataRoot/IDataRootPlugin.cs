namespace FM39hz.DataCatalyst.DataRoot;

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

public sealed class PluginResult {
    public string SourceCode { get; set; } = "";
    public ImmutableArray<Diagnostic> Diagnostics { get; set; } = ImmutableArray<Diagnostic>.Empty;
    public ImmutableDictionary<string, string> AdditionalSources { get; set; }
        = ImmutableDictionary<string, string>.Empty;
}

public sealed class PluginContext {
    public string FilePath { get; }
    public string RelativePath { get; }
    public string Content { get; }
    public string Namespace { get; }
    public string RootPrefix { get; }
    public SourceProductionContext Spc { get; }

    public PluginContext(string filePath, string relativePath, string content,
        string ns, string rootPrefix, SourceProductionContext spc) {
        FilePath = filePath;
        RelativePath = relativePath;
        Content = content;
        Namespace = ns;
        RootPrefix = rootPrefix;
        Spc = spc;
    }
}

public interface IDataRootPlugin {
    string Name { get; }
    bool CanHandle(string relativePath, string content);
    PluginResult Process(PluginContext ctx);
}
