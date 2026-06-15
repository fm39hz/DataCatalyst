namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System.IO;

public sealed class ModSource {
    public string Name { get; }
    public string Directory { get; }
    public int Priority { get; }

    public ModSource(string directory, int priority = 0) {
        Name = Path.GetFileName(directory.TrimEnd('/', '\\')) ?? "Source";
        Directory = directory;
        Priority = priority;
    }

    public static ModSource From(string directory, int priority = 0)
        => new(directory, priority);
}
