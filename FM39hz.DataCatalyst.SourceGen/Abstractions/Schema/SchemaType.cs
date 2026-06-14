namespace FM39hz.DataCatalyst.Abstractions;

using System;
using System.Collections.Immutable;

/// <summary>
///     Describes the C# type for a single column. Three flavours exist:
///     <list type="bullet">
///         <item><see cref="IsPrimitive" />: a built-in primitive name (e.g. <c>"int"</c>, <c>"string"</c>).</item>
///         <item><see cref="IsArray" />: a homogeneous array; <see cref="ArrayElement" /> describes the item type.</item>
///         <item>
///             <see cref="IsObject" />: a nested record; <see cref="ObjectColumns" /> lists its sub-columns.
///             The generated nested struct is named <c>{ColumnPascalCase}Row</c>.
///         </item>
///     </list>
/// </summary>
public sealed class SchemaType : IEquatable<SchemaType> {
	public bool IsPrimitive { get; private set; }
	public bool IsArray { get; private set; }
	public bool IsObject { get; private set; }
	public string? Primitive { get; private set; }
	public SchemaType? ArrayElement { get; private set; }
	public ImmutableArray<SchemaColumn> ObjectColumns { get; private set; } = ImmutableArray<SchemaColumn>.Empty;
	public string? OwningColumnName { get; set; }

	public static SchemaType OfPrimitive(string fullName) => new() { IsPrimitive = true, Primitive = fullName };
	public static SchemaType PrimitiveLiteral(string fullName) => new() { IsPrimitive = true, Primitive = fullName };
	public static SchemaType OfArray(SchemaType element) => new() { IsArray = true, ArrayElement = element };
	public static SchemaType OfObject(ImmutableArray<SchemaColumn> columns) => new() { IsObject = true, ObjectColumns = columns };

	public bool Equals(SchemaType? other) {
		if (other is null) {
			return false;
		}

		if (IsPrimitive && other.IsPrimitive) {
			return string.Equals(Primitive, other.Primitive, StringComparison.Ordinal);
		}

		if (IsArray && other.IsArray) {
			return ArrayElement!.Equals(other.ArrayElement!);
		}

		if (IsObject && other.IsObject) {
			if (ObjectColumns.Length != other.ObjectColumns.Length) {
				return false;
			}

			for (var i = 0; i < ObjectColumns.Length; i++) {
				if (!string.Equals(ObjectColumns[i].Name, other.ObjectColumns[i].Name, StringComparison.Ordinal)) {
					return false;
				}

				if (!ObjectColumns[i].Type.Equals(other.ObjectColumns[i].Type)) {
					return false;
				}
			}

			return true;
		}

		return false;
	}

	public override bool Equals(object? obj) => obj is SchemaType st && Equals(st);

	public override int GetHashCode() {
		if (IsPrimitive) {
			return Primitive?.GetHashCode() ?? 0;
		}

		if (IsArray) {
			return unchecked(17 * 31 + (ArrayElement?.GetHashCode() ?? 0));
		}

		if (IsObject) {
			var h = 17;
			foreach (var c in ObjectColumns) {
				h = unchecked(h * 31 + c.Name.GetHashCode());
			}

			return h;
		}

		return 0;
	}
}
