using DataCatalyst.Loader;

namespace DataCatalyst.Schema;

/// <summary>Format-agnostic schema loader. Implementations: JsonSchemaLoader,
/// YamlSchemaLoader, etc.</summary>
public interface ISchemaLoader
{
    /// <summary>Load schema from content string (format-specific).</summary>
    SchemaRegistry LoadSchema(string content);

    /// <summary>Load schema from a file path.</summary>
    SchemaRegistry LoadSchemaFile(string path);

    /// <summary>Load schema from all files in a directory.</summary>
    SchemaRegistry LoadSchemaDirectory(string path);

    /// <summary>Load raw entries, validated against schema.
    /// Returns validation errors in LoadResult.Diagnostics.</summary>
    LoadResult LoadEntries(string content, string key, SchemaRegistry schema);
}
