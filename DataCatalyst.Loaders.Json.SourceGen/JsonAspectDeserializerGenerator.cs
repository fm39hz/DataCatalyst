// No-op — main AspectGenerator handles all struct gen + deserializers.
// This file exists to keep the project compilable.
namespace DataCatalyst.Loaders.Json;

using Microsoft.CodeAnalysis;

[Generator]
public sealed class JsonAspectDeserializerGenerator : IIncrementalGenerator {
	public void Initialize(IncrementalGeneratorInitializationContext context) { }
}
