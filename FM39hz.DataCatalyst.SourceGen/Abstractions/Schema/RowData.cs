namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Generic;

/// <summary>
///     One JSON entry resolved into a (key, columns) pair.
///     <para>
///         <see cref="Key" /> is the C# identifier that becomes both the synthetic enum member name and the
///         <c>public static readonly</c> field name on the generated partial type. It must be a valid C# identifier.
///     </para>
///     <para>
///         <see cref="Values" /> is column-name → cached value. Lookup is case-insensitive so JSON files using
///         camelCase resolve against PascalCase template members without any manual mapping.
///     </para>
/// </summary>
public sealed class RowData {
	public string Key { get; }
	public Dictionary<string, JsonValueModel> Values { get; }

	public RowData(string key, Dictionary<string, JsonValueModel> values) {
		Key = key;
		Values = values;
	}
}
