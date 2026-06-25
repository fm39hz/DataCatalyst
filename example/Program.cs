using System;
using System.IO;
using Example;
using DataCatalyst.World;
using DataCatalyst.Pipeline;
using DataCatalyst.Loaders;
using DataCatalyst.Generated;

var root = AppContext.BaseDirectory;
while (root != null && !Directory.Exists(Path.Combine(root, "Data")))
    root = Directory.GetParent(root)?.FullName!;
if (string.IsNullOrEmpty(root))
    root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "example");

var world = new Pipeline()
    .LoadSchemaFrom(new JsonSchemaLoader(), Path.Combine(root, "."))
    .AddSource("Base", new JsonDataLoader(), Path.Combine(root, "Data"), s =>
        { s.Priority = 0; s.MergePolicy = MergePolicy.Patch; })
    .AddSource("DLC", new JsonDataLoader(), Path.Combine(root, "DLC"), s =>
        { s.Priority = 1; s.MergePolicy = MergePolicy.Patch; })
    .AddSource("Mods", new JsonDataLoader(), Path.Combine(root, "Mods"), s =>
        { s.Priority = 2; s.MergePolicy = MergePolicy.FieldPatch; })
    .Build(out var diagnostics);

Console.WriteLine($"Concepts: {world.Schema?.ConceptAspects.Count}");
Console.WriteLine($"Aspects:  {world.Schema?.Aspects.Count}");

if (world.Schema != null)
    foreach (var kv in world.Schema.ConceptAspects)
    {
        var cname = world.Schema.TryGetConceptName(kv.Key, out var n) ? n! : "?";
        var anames = kv.Value.Select(id => world.Schema.TryGetAspectName(id, out var a) ? a! : "?").ToArray();
        Console.WriteLine($"  {cname}: [{string.Join(", ", anames)}]");
    }

Console.WriteLine($"\n=== World ===");
var arthur = world.FromConcept<Creature>().At<Arthur>();
Console.WriteLine($"Arthur HP:   {arthur.Take<Health>().Current}/{arthur.Take<Health>().Max}");
Console.WriteLine($"Arthur Mana: {arthur.Take<Mana>().Current}/{arthur.Take<Mana>().Max}");

var goblin = world.FromConcept<Creature>().At<Goblin>();
Console.WriteLine($"Goblin HP:   {goblin.Take<Health>().Current}/{goblin.Take<Health>().Max}");
Console.WriteLine($"Goblin XP:   {world.FromConcept<Enemy>().At<Goblin>().Take<ExperienceReward>().Amount}");

Console.WriteLine($"\nDone.");
