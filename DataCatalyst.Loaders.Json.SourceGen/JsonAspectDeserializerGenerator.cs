// No-op — main AspectGenerator handles all struct gen + deserializers.
// This file exists to keep the project compilable.
using Microsoft.CodeAnalysis;

namespace DataCatalyst.Loaders.Json;

[Generator]
public sealed class JsonAspectDeserializerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) { }
}
