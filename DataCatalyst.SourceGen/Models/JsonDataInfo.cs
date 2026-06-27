namespace DataCatalyst.SourceGen.Models;

public sealed record BeingFieldInfo(string Name, string[] NestedFields);

public sealed record BeingEntry(string Key, BeingFieldInfo[] Fields, string[] Concepts);

public sealed record JsonDataInfo(BeingEntry[] Beings);
