using Microsoft.CodeAnalysis;

namespace DataCatalyst.Loaders.Json;

[Generator]
public sealed class JsonAspectDeserializerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // No-op: Deserialization is handled dynamically at runtime via fallback in AspectTypeRegistry
    }
}
