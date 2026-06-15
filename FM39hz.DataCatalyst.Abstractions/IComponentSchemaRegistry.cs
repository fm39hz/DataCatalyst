namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Generic;

public sealed class ComponentSchemaField {
    public string Name { get; }
    public string Type { get; }
    public ComponentSchemaField(string name, string type) {
        Name = name;
        Type = type;
    }
}

public sealed class ComponentSchema {
    public string Name { get; }
    public IReadOnlyList<ComponentSchemaField> Fields { get; }
    public ComponentSchema(string name, IReadOnlyList<ComponentSchemaField> fields) {
        Name = name;
        Fields = fields;
    }
}

public interface IComponentSchemaRegistry {
    void Register(ComponentSchema schema);
    bool TryGet(string name, out ComponentSchema? schema);
    IEnumerable<ComponentSchema> GetAll();
}
