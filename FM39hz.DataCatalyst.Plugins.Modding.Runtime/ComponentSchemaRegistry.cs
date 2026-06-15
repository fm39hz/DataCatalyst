namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System;
using System.Collections.Generic;
using FM39hz.DataCatalyst.Abstractions;

public sealed class ComponentSchemaRegistry : IComponentSchemaRegistry {
    private readonly Dictionary<string, ComponentSchema> _schemas = new();
    private readonly object _lock = new();

    public void Register(ComponentSchema schema) {
        lock (_lock) {
            _schemas[schema.Name] = schema;
        }
    }

    public bool TryGet(string name, out ComponentSchema? schema) {
        lock (_lock) {
            return _schemas.TryGetValue(name, out schema);
        }
    }

    public IEnumerable<ComponentSchema> GetAll() {
        lock (_lock) {
            return new List<ComponentSchema>(_schemas.Values);
        }
    }
}
