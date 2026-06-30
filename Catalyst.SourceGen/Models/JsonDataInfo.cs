namespace Catalyst.SourceGen.Models;

public sealed record BeingFieldInfo(string Name, string[] NestedFields);

public sealed record BeingEntry(string Key, BeingFieldInfo[] Fields, string[] Concepts, string? Description = null);

public sealed record JsonDataInfo(BeingEntry[] Beings);
