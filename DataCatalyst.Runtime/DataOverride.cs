namespace DataCatalyst.Runtime;

public sealed class DataOverride {
    public string Target { get; set; } = "";
    public string RawJson { get; set; } = "";  // raw JSON object, no boxing
}
