namespace DataCatalyst.Core;

using DataCatalyst.Abstractions;

/// <summary>Well-known component for declaring inheritance parent keys (JSON: "inherits").</summary>
[DataComponent]
public readonly partial struct Inherits {
	public string[] Value { get; init; }
}

/// <summary>Well-known component for loading order (JSON: "layer").</summary>
[DataComponent]
public readonly partial struct Layer {
	public int Value { get; init; }
}

/// <summary>Well-known component for concept membership (JSON: "Concept").</summary>
[DataComponent]
public readonly partial struct Concept {
	public string Value { get; init; }
}
