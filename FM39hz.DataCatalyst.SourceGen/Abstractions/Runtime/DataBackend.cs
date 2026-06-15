namespace FM39hz.DataCatalyst.Abstractions;

using System;

/// <summary>
/// Describes which runtime data backends the source generator should emit code for.
/// <see cref="None" /> preserves the current behaviour (compile-time-only <c>FrozenDictionary</c>).
/// </summary>
[Flags]
public enum DataBackend {
	None   = 0,
	Json   = 1 << 0,
	Sqlite = 1 << 1,
	All    = Json | Sqlite,
}
