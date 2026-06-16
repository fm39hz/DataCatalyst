namespace DataCatalyst.Runtime;

using System.IO;

public sealed class DataSource {
    public string Name { get; }
    public string Directory { get; }
    public int Priority { get; }

    public DataSource(string directory, int priority = 0) {
        Name = Path.GetFileName(directory.TrimEnd('/', '\\')) ?? "Source";
        Directory = directory;
        Priority = priority;
    }

    public static DataSource From(string directory, int priority = 0) => new(directory, priority);
}
