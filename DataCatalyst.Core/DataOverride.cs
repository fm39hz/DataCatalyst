namespace DataCatalyst.Core;

/// <summary>Runtime override targeting a specific entry.</summary>
public sealed record DataOverride {
	/// <summary>Entry key to override.</summary>
	public string Target { get; init; } = string.Empty;

	/// <summary>Raw JSON payload for the override.</summary>
	public string RawJson { get; init; } = string.Empty;
}
