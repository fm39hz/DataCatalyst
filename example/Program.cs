using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DataCatalyst;
using DataCatalyst.Loaders;
using DataCatalyst.Knowledge;
using DataCatalyst.Pipeline;
using DataCatalyst.Pipeline.Stages;
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

// Create registries and populate from generated code
var registries = new RegistrySet();
DataCatalystRegistries.Populate(registries);

var knowledge = new Pipeline(registries)
	.AddSource("Base", new JsonDataLoader(registries.Beings), Path.Combine(root, "Data"))
	.AddSource("DLC", new JsonDataLoader(registries.Beings), Path.Combine(root, "DLC"))
	.AddSource("Mods", new JsonDataLoader(registries.Beings), Path.Combine(root, "Mods"))
	.AddOntology(Path.Combine(root, "Data"), ["concepts.json", "aspects.json", "relations.json"])
	.AddOntology(Path.Combine(root, "DLC"), ["*/concepts.json", "*/aspects.json"])
	.AddBaker(new StateEngineBaker(registries.Beings))
	.Run(out var diagnostics);

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
Console.WriteLine($"Arthur HP:   {knowledge.Of<Creature, Health>(typeof(Arthur)).Current}/{knowledge.Of<Creature, Health>(typeof(Arthur)).Max}");
Console.WriteLine($"Arthur Mana: {knowledge.Of<Creature, Mana>(typeof(Arthur)).Current}/{knowledge.Of<Creature, Mana>(typeof(Arthur)).Max}");

Console.WriteLine($"Goblin HP:   {knowledge.Of<Creature, Health>(typeof(Goblin)).Current}/{knowledge.Of<Creature, Health>(typeof(Goblin)).Max}");
Console.WriteLine($"Goblin XP:   {knowledge.Of<Enemy, ExperienceReward>(typeof(Goblin)).Amount}");

// === StateEngine Simulation ===
Console.WriteLine($"\n=== StateEngine Simulation ===");
var stateGroupDef = knowledge.Of<State, StateGroup>(typeof(BasicAI));
var baked = knowledge.GetBaked<BakedStateGroup, DataCatalyst.Generated.BasicAI>();

Ref<State> currentState = baked.DefaultState;
Console.WriteLine($"Initial State: {currentState}");

var viableStatesList = new List<Ref<State>>();
foreach (var name in stateGroupDef.States)
{
	var idx = knowledge.GetBeingIndex<DataCatalyst.Generated.BasicAI>();
	if (idx >= 0)
	{
		var targetPool = knowledge.GetPool(typeof(State));
		if (targetPool != null)
		{
			// Use knowledge to resolve state types
			foreach (var r in registries.Beings.All)
			{
				if (r.BeingType.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					viableStatesList.Add(new Ref<State>(r.BeingType));
					break;
				}
			}
		}
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
	var evaluator = new StateEngineEvaluator();
	var evalResult = evaluator.Evaluate(
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

struct SimulationSensorReader : StateEngineEvaluator.ISensorReader
{
	public float Timer;

	public float ReadSensor(Ref<Sensor> sensor)
	{
		if (sensor.BeingType == typeof(DataCatalyst.Generated.Timer))
		{
			return Timer;
		}
		return 0f;
	}
}
