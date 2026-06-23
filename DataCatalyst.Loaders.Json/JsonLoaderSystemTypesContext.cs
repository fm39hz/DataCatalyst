namespace DataCatalyst.Loaders;

using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, IncludeFields = true)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(float?))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(global::DataCatalyst.Core.Inherits))]
[JsonSerializable(typeof(global::DataCatalyst.Core.Layer))]
[JsonSerializable(typeof(global::DataCatalyst.Core.Concept))]
internal partial class JsonLoaderSystemTypesContext : JsonSerializerContext {}
