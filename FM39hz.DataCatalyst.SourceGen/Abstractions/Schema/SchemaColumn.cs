namespace FM39hz.DataCatalyst.Abstractions;

/// <summary>
///     A single (logical) column of a row. <see cref="Name" /> is the original JSON key (case-insensitive
///     for lookup). <see cref="MemberName" /> is the PascalCase C# identifier emitted into the generated source.
/// </summary>
public sealed class SchemaColumn {
	public string Name { get; }
	public string MemberName { get; }
	public SchemaType Type { get; }

	public SchemaColumn(string name, SchemaType type) {
		Name = name;
		MemberName = ToPascalCase(name);
		Type = type;
		if (type.IsObject && type.OwningColumnName is null) {
			type.OwningColumnName = MemberName;
		}

		if (type.IsArray && type.ArrayElement!.IsObject && type.ArrayElement.OwningColumnName is null) {
			type.ArrayElement.OwningColumnName = MemberName;
		}
	}

	private static string ToPascalCase(string name) {
		if (string.IsNullOrEmpty(name)) {
			return name;
		}

		if (char.IsUpper(name[0])) {
			return name;
		}

		return char.ToUpperInvariant(name[0]) + name.Substring(1);
	}
}
