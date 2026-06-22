using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Core;
using DataCatalyst.Loaders;

// Setup environment
var env = new DataCatalystEnvironment();

Console.WriteLine("=== DataCatalyst Example ===\n");

// Load data from JSON files
var dataPath = Path.Combine(AppContext.BaseDirectory, "Data");

// Fallback: use source tree data path when running from build output
if (!Directory.Exists(dataPath)) {
    dataPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data");
}

Console.WriteLine($"Loading data from: {dataPath}");

var loader = new JsonDataLoader(
    new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    },
    env
);

var loadResult = loader.LoadDirectory(dataPath);

// Show diagnostics
if (loadResult.Diagnostics.Count > 0) {
    Console.WriteLine("\nDiagnostics:");
    foreach (var diag in loadResult.Diagnostics) {
        Console.WriteLine($"  - {diag}");
    }
}

Console.WriteLine($"\nLoaded {loadResult.Entries.Count} entries");

// Build catalog via pipeline
var pipeline = new DataPipeline(env);
pipeline.Load(loader, dataPath);
var catalog = pipeline.Build();

Console.WriteLine($"Built catalog with {catalog.Entries.Count} entries\n");

// Show loaded entries - inspect components dynamically
Console.WriteLine("--- Loaded Entries ---");
foreach (var entry in catalog.Entries) {
    Console.WriteLine($"\nEntry: {entry.Key} ({entry.Value.Components.Count} components)");
    foreach (var comp in entry.Value.Components) {
        Console.WriteLine($"  {comp.Key.Name}: {comp.Value}");
    }
}

// Show concept groups
Console.WriteLine("\n--- Concepts ---");
var conceptPlugin = env.Plugins.Get<GameConceptPlugin>();
if (conceptPlugin != null) {
    // List all registered concepts
    foreach (var conceptName in conceptPlugin.Registry.ConceptNames) {
        Console.WriteLine($"  Concept: {conceptName}");
    }
} else {
    Console.WriteLine("  No GameConceptPlugin registered");
}

Console.WriteLine("\n=== Example Complete ===");
