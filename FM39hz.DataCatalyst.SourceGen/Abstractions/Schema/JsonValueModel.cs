namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Generic;
using System.Text.Json;

/// <summary>
///     A reader-agnostic snapshot of a JSON value. DataCatalyst plugins receive <see cref="JsonValueModel" />
///     instances instead of <see cref="JsonElement" /> so the in-memory representation is decoupled from
///     the upstream reader (today: <see cref="JsonDocument" />; tomorrow: <see cref="Utf8JsonReader" />
///     streaming, or a binary-format reader).
/// </summary>
/// <remarks>
///     Number values are eagerly resolved into both <see cref="NumberAsLong" /> and <see cref="NumberAsDouble" />
///     so plugins do not need to re-parse raw text. <see cref="NumberIsIntegral" /> tells primitive rules whether
///     they may pick the integer-typed plugin (<c>int</c>/<c>long</c>) over <c>float</c>/<c>double</c>.
/// </remarks>
public sealed class JsonValueModel {
	public JsonValueKind Kind { get; private set; }
	public string? StringValue { get; private set; }
	public double NumberAsDouble { get; private set; }
	public long NumberAsLong { get; private set; }
	public bool NumberIsIntegral { get; private set; }
	public bool NumberFitsInt { get; private set; }
	public List<JsonValueModel>? ArrayItems { get; private set; }
	public Dictionary<string, JsonValueModel>? ObjectMembers { get; private set; }

	/// <summary>
	///     Materialises every property of <paramref name="obj" /> (which must be <see cref="JsonValueKind.Object" />)
	///     into a case-insensitive dictionary. Case-insensitive lookup tolerates camelCase JSON keys against
	///     PascalCase C# member names without manual mapping.
	/// </summary>
	public static Dictionary<string, JsonValueModel> CloneObject(JsonElement obj) {
		var d = new Dictionary<string, JsonValueModel>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var p in obj.EnumerateObject()) {
			d[p.Name] = From(p.Value);
		}

		return d;
	}

	public static JsonValueModel From(JsonElement el) {
		switch (el.ValueKind) {
			case JsonValueKind.String:
				return new JsonValueModel { Kind = JsonValueKind.String, StringValue = el.GetString() };
			case JsonValueKind.True:
				return new JsonValueModel { Kind = JsonValueKind.True };
			case JsonValueKind.False:
				return new JsonValueModel { Kind = JsonValueKind.False };
			case JsonValueKind.Null:
				return new JsonValueModel { Kind = JsonValueKind.Null };
			case JsonValueKind.Number: {
					var raw = el.GetRawText();
					var integral = raw.IndexOf('.') < 0 && raw.IndexOf('e') < 0 && raw.IndexOf('E') < 0;
					var asDouble = el.GetDouble();
					long asLong = 0;
					var fitsInt = false;
					if (integral && el.TryGetInt64(out var v)) {
						asLong = v;
						fitsInt = v is >= int.MinValue and <= int.MaxValue;
					}

					return new JsonValueModel {
						Kind = JsonValueKind.Number,
						NumberIsIntegral = integral,
						NumberAsDouble = asDouble,
						NumberAsLong = asLong,
						NumberFitsInt = fitsInt,
					};
				}

			case JsonValueKind.Array: {
					var list = new List<JsonValueModel>();
					foreach (var item in el.EnumerateArray()) {
						list.Add(From(item));
					}

					return new JsonValueModel { Kind = JsonValueKind.Array, ArrayItems = list };
				}

			case JsonValueKind.Object: {
					var dict = new Dictionary<string, JsonValueModel>(System.StringComparer.Ordinal);
					foreach (var p in el.EnumerateObject()) {
						dict[p.Name] = From(p.Value);
					}

					return new JsonValueModel { Kind = JsonValueKind.Object, ObjectMembers = dict };
				}

			case JsonValueKind.Undefined:
			default:
				return new JsonValueModel { Kind = JsonValueKind.Undefined };
		}
	}
}
