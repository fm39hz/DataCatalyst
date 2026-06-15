namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System;
using System.Collections.Generic;

public sealed class ModContentEntry {
    public string Type { get; }
    public string File { get; }
    public string? Target { get; }
    public ModContentEntry(string type, string file, string? target = null) {
        Type = type;
        File = file;
        Target = target;
    }
}

public sealed class ModDependency {
    public string Id { get; }
    public string Version { get; }
    public ModDependency(string id, string version) {
        Id = id;
        Version = version;
    }
}

public sealed class ModManifest {
    public string Id { get; }
    public Version Version { get; }
    public Version GameVersion { get; }
    public IReadOnlyList<ModDependency> Dependencies { get; }
    public IReadOnlyList<ModContentEntry> Content { get; }
    public string Directory { get; }

    public ModManifest(
        string id,
        Version version,
        Version gameVersion,
        IReadOnlyList<ModDependency>? dependencies = null,
        IReadOnlyList<ModContentEntry>? content = null,
        string directory = "") {

        Id = id;
        Version = version;
        GameVersion = gameVersion;
        Dependencies = dependencies ?? Array.Empty<ModDependency>();
        Content = content ?? Array.Empty<ModContentEntry>();
        Directory = directory;
    }
}
