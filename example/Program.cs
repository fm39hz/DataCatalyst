using System;
using System.IO;
using Example;
using DataCatalyst.World;
using DataCatalyst.Pipeline;
using DataCatalyst.Loaders;
using DataCatalyst.Generated;
using DataCatalyst.Generated.Entries;

var root = AppContext.BaseDirectory;
while (root != null && !Directory.Exists(Path.Combine(root, "Data")))
    root = Directory.GetParent(root)?.FullName!;

if (string.IsNullOrEmpty(root))
    root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "example");

var world = new Pipeline()
    .AddSource("Base", new JsonDataLoader(), Path.Combine(root, "Data"), s =>
        { s.Priority = 0; s.MergePolicy = MergePolicy.Patch; })
    .AddSource("DLC", new JsonDataLoader(), Path.Combine(root, "DLC"), s =>
        { s.Priority = 1; s.MergePolicy = MergePolicy.Patch; })
    .AddSource("Mods", new JsonDataLoader(), Path.Combine(root, "Mods"), s =>
        { s.Priority = 2; s.MergePolicy = MergePolicy.FieldPatch; })
    .Build(out var diagnostics);

foreach (var d in diagnostics.Items)
    Console.WriteLine($"  {d}");

Console.WriteLine($"\n=== World API ===");

// Entry access pattern — primary, no concept needed
var arthur = world.FromConcept<Creature>().At<Arthur>();
Console.WriteLine($"Arthur HP:   {arthur.Take<Health>().Current}/{arthur.Take<Health>().Max}");
Console.WriteLine($"Arthur Mana: {arthur.Take<Mana>().Current}/{arthur.Take<Mana>().Max}");
Console.WriteLine($"Arthur Name: '{arthur.Take<Label>().Name}'");
Console.WriteLine($"Arthur DMG:  {arthur.Take<CombatStats>().BaseDamage}");

var goblin = world.FromConcept<Creature>().At<Goblin>();
Console.WriteLine($"Goblin HP:   {goblin.Take<Health>().Current}/{goblin.Take<Health>().Max}");
Console.WriteLine($"Goblin Mana: {goblin.Take<Mana>().Current}/{goblin.Take<Mana>().Max}");
Console.WriteLine($"Goblin DMG:  {goblin.Take<CombatStats>().BaseDamage}");
Console.WriteLine($"Goblin XP:   {world.FromConcept<Enemy>().At<Goblin>().Take<ExperienceReward>().Amount}");

var dragon = world.FromConcept<Creature>().At<Dragon>();
Console.WriteLine($"Dragon HP:   {dragon.Take<Health>().Current}/{dragon.Take<Health>().Max}");

Console.WriteLine($"\n✅ Done.");
