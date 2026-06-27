using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DataCatalyst;
using DataCatalyst.Loaders;
using DataCatalyst.Knowledge;
using DataCatalyst.Pipeline;
using DataCatalyst.Generated;
using DataCatalyst.Composition;
using DataCatalyst.Registry;
using DataCatalyst.StateEngine.Core;
using DataCatalyst.StateEngine.Models;

var root = AppContext.BaseDirectory;
while (root != null && !Directory.Exists(Path.Combine(root, "Data")))
	root = Directory.GetParent(root)?.FullName!;
if (string.IsNullOrEmpty(root))
	root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "example");

var knowledge = new Pipeline()
	.Load(root, new JsonDataLoader())
	.AddBaker(new StateEngineBaker())
	.Build(out var diagnostics);

if (knowledge == null)
{
	Console.WriteLine("Pipeline build failed!");
	foreach (var item in diagnostics.Items)
	{
		Console.WriteLine(item);
	}
	return;
}

Console.WriteLine($"Concepts: {knowledge.Schema?.ConceptAspects.Count}");
Console.WriteLine($"Aspects:  {knowledge.Schema?.Aspects.Count}");

if (knowledge.Schema != null)
	foreach (var kv in knowledge.Schema.ConceptAspects)
	{
		var cname = knowledge.Schema.TryGetConceptName(kv.Key, out var n) ? n! : "?";
		var anames = kv.Value.Select(id => knowledge.Schema.TryGetAspectName(id, out var a) ? a! : "?").ToArray();
		Console.WriteLine($"  {cname}: [{string.Join(", ", anames)}]");
	}

Console.WriteLine($"\n=== Knowledge ===");
var arthur = knowledge.Of<Creature>().At<Arthur>();
Console.WriteLine($"Arthur HP:   {arthur.Take<Health>().Current}/{arthur.Take<Health>().Max}");
Console.WriteLine($"Arthur Mana: {arthur.Take<Mana>().Current}/{arthur.Take<Mana>().Max}");

var goblin = knowledge.Of<Creature>().At<Goblin>();
Console.WriteLine($"Goblin HP:   {goblin.Take<Health>().Current}/{goblin.Take<Health>().Max}");
Console.WriteLine($"Goblin XP:   {knowledge.Of<Enemy>().At<Goblin>().Take<ExperienceReward>().Amount}");

// === StateEngine Simulation ===
Console.WriteLine($"\n=== StateEngine Simulation ===");
var stateGroupDef = knowledge.Of<GameState>().At<BasicAI>().Take<StateGroup>();
var baked = knowledge.GetBaked<BakedStateGroup, DataCatalyst.Generated.BasicAI>();

Ref<State> currentState = baked.DefaultState;
Console.WriteLine($"Initial State: {currentState}");

var viableStatesList = new List<Ref<State>>();
foreach (var name in stateGroupDef.States)
{
	Type? stateType = null;
	foreach (var r in BeingRegistry.All)
	{
		if (r.BeingType.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
		{
			stateType = r.BeingType;
			break;
		}
	}
	if (stateType != null)
	{
		viableStatesList.Add(new Ref<State>(stateType));
	}
}
var viableStates = viableStatesList.ToArray();

float timer = 0.0f;

for (int step = 1; step <= 10; step++)
{
	timer += 0.6f;
	if (timer > 5.5f)
	{
		timer = 0.0f; // reset timer loop
	}

	var reader = new SimulationSensorReader { Timer = timer };
	var evalResult = StateEngineEvaluator.Evaluate(
		currentState,
		baked,
		viableStates,
		ref reader
	);

	if (evalResult.HasValue && !evalResult.TargetState.Equals(currentState))
	{
		var oldState = currentState.ToString();
		currentState = evalResult.TargetState;
		var newState = currentState.ToString();
		Console.WriteLine($"Step {step:D2}: Timer = {timer:F1} -> State changed from {oldState} to {newState}");
	}
	else
	{
		Console.WriteLine($"Step {step:D2}: Timer = {timer:F1} -> State remains {currentState}");
	}
}

Console.WriteLine($"\nDone.");

struct SimulationSensorReader : ISensorReader
{
	public float Timer;

	public float ReadSensor(Ref<Sensor> sensor)
	{
		if (sensor == typeof(DataCatalyst.Generated.Timer))
		{
			return Timer;
		}
		return 0f;
	}
}
