using DataCatalyst.Abstractions;

[assembly: AotContext(
	contextName: "DataCatalystComponentsJsonContext",
	baseType: "global::System.Text.Json.Serialization.JsonSerializerContext",
	attributeType: "global::System.Text.Json.Serialization.JsonSerializableAttribute",
	registerMethod: "global::DataCatalyst.Loaders.JsonResolverRegistry.Register",
	ExtraClassAttributes = new[] {
		"global::System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, IncludeFields = true)"
	}
)]
