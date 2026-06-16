namespace FM39hz.DataCatalyst.Runtime;

using System.Collections.Generic;

public sealed class DataOverride {
    public string Target { get; set; } = "";
    public Dictionary<string, object> Fields { get; set; } = new();
}
