namespace FM39hz.DataCatalyst.Abstractions;

using System;

[Flags]
public enum DataBackend {
	None = 0,
	Json = 1 << 0,
	Sqlite = 1 << 1,
	All = Json | Sqlite,
}
