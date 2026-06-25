using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Example;
using DataCatalyst.World;
using DataCatalyst.Pipeline;
using DataCatalyst.Loaders;
using DataCatalyst.Generated;
using DataCatalyst.Composition;
using DataCatalyst.StateEngine.Core;
using DataCatalyst.StateEngine.Models;

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

if (world == null)
{
    Console.WriteLine("Pipeline build failed!");
    foreach (var item in diagnostics.Items)
    {
        Console.WriteLine(item);
    }
    return;
}

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

// === StateEngine Simulation ===
Console.WriteLine($"\n=== StateEngine Simulation ===");
var stateGroupDef = world.FromConcept<GameState>().At<BasicAI>().Take<StateGroup>();
var baked = StateEngineBaker.Bake(stateGroupDef, world);

var sortedStateNames = stateGroupDef.States.Keys.OrderBy(s => s).ToList();
var idToName = new Dictionary<int, string>();
for (int i = 0; i < sortedStateNames.Count; i++)
{
    idToName[i + 1] = sortedStateNames[i];
}

int currentStateId = baked.DefaultStateId;
Console.WriteLine($"Initial State: {idToName[currentStateId]} (ID: {currentStateId})");

var viableStates = new HashSet<int>(idToName.Keys);
float timer = 0.0f;

for (int step = 1; step <= 10; step++)
{
    timer += 0.6f;
    if (timer > 5.5f)
    {
        timer = 0.0f; // reset timer loop
    }

    var evalResult = StateEngineEvaluator.Evaluate(
        currentStateId,
        baked,
        viableStates,
        signalId => {
            if (signalId == "Timer".GetHashCode())
            {
                return timer;
            }
            return 0f;
        }
    );

    if (evalResult.HasValue && evalResult.TargetStateId != currentStateId)
    {
        var oldState = idToName[currentStateId];
        currentStateId = evalResult.TargetStateId;
        var newState = idToName[currentStateId];
        Console.WriteLine($"Step {step:D2}: Timer = {timer:F1} -> State changed from {oldState} to {newState}");
    }
    else
    {
        Console.WriteLine($"Step {step:D2}: Timer = {timer:F1} -> State remains {idToName[currentStateId]}");
    }
}

Console.WriteLine($"\nDone.");
