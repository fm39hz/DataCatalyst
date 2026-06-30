using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DataCatalyst;
using DataCatalyst.Loaders;
using DataCatalyst.Knowledge;
using DataCatalyst.Pipeline;
using DataCatalyst.Registry;
using DataCatalyst.Generated;
using DataCatalyst.StateEngine;

var root = AppContext.BaseDirectory;
while (root != null && !Directory.Exists(Path.Combine(root, "Data")))
	root = Directory.GetParent(root)?.FullName!;
if (string.IsNullOrEmpty(root))
	root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "example");

var registries = new RegistrySet();
DataCatalystRegistries.Populate(registries);

var knowledge = new Pipeline(registries)
	.AddSource("Base", new JsonDataLoader(registries.Beings), Path.Combine(root, "Data"))
	.AddSource("DLC", new JsonDataLoader(registries.Beings), Path.Combine(root, "DLC"))
	.AddSource("Mods", new JsonDataLoader(registries.Beings), Path.Combine(root, "Mods"))
	.Run(out var diagnostics);

if (knowledge == null)
{
	Console.WriteLine("Pipeline build failed!");
	foreach (var item in diagnostics.Items)
		Console.WriteLine($"  {item}");
	return;
}

Console.WriteLine($"Concepts: {knowledge.Schema?.ConceptAspects.Count}");
Console.WriteLine($"Aspects:  {knowledge.Schema?.Aspects.Count}\n");

if (knowledge.Schema != null)
	foreach (var kv in knowledge.Schema.ConceptAspects)
	{
		var cname = knowledge.Schema.TryGetConceptName(kv.Key, out var n) ? n! : "?";
		var anames = kv.Value.Select(id => knowledge.Schema.TryGetAspectName(id, out var a) ? a! : "?").ToArray();
		Console.WriteLine($"  {cname}: [{string.Join(", ", anames)}]");
	}

// Query RoboticKnight
Console.WriteLine($"\n=== Knowledge ===");
try
{
	var rig = knowledge.Of<Humanoid, SkeletonRig>(typeof(RoboticKnight));
	Console.WriteLine($"RoboticKnight — Bones: {rig.BoneCount}, Bipedal: {rig.IsBipedal}");
	var power = knowledge.Of<Mechanical, BatteryCapacity>(typeof(RoboticKnight));
	Console.WriteLine($"RoboticKnight — Power: {power.MaxJoules} J, Efficiency: {power.Efficiency:P}");
}
catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }

// Query DLC mount
Console.WriteLine($"\n=== DLC ===");
var mountCount = registries.Beings.All.Count(r => r.Concepts.Any(c => c.Name == "Mount"));
Console.WriteLine($"Mounts: {mountCount}");
foreach (var r in registries.Beings.All)
	if (r.Concepts.Any(c => c.Name == "Mount"))
		Console.WriteLine($"  {r.BeingType.Name}");

// State Engine — đọc ABC trực tiếp từ Knowledge
Console.WriteLine($"\n=== StateEngine (ABC) ===");
try
{
	// StateGroup từ entity
	var sg = knowledge.Of<State, StateGroup>(typeof(RoboticKnight));
	Console.WriteLine($"Default state: {sg.DefaultState}");
	Console.WriteLine($"Total states:  {sg.States.Count}");

	// Duyệt từng state, in links + desirability
	foreach (var s in sg.States)
	{
		var links = StateEngine.GetLinks(knowledge, s);
		var linkCount = links.Links?.Count ?? 0;
		Console.WriteLine($"  {s}: {linkCount} link(s)");

		var des = StateEngine.GetDesirability(knowledge, s);
		Console.WriteLine($"    desirability: priority={des.Priority} influences={des.Influences?.Count ?? 0}");

		if (links.Links != null)
			foreach (var lnk in links.Links)
			{
				var hasGate = lnk.Gate != null ? " (has gate)" : "";
				Console.WriteLine($"    -> {lnk.Target}{hasGate}");
			}
	}

	// Evaluate: St_Idle với sensor = 15 → nên chuyển sang St_Patrol
	Console.WriteLine($"\n=== Evaluate transitions ===");
	var idleRef = new Ref<State>(typeof(St_Idle));
	var idleLinks = StateEngine.GetLinks(knowledge, idleRef);

	// FSM mode (first valid link)
	var result = StateEngine.Evaluate(
		CollectionsMarshal.AsSpan(idleLinks.Links ?? []),
		CollectionsMarshal.AsSpan(sg.States),
		idleRef,
		sensor => sensor.BeingType.Name == "S_DistanceSensor" ? 15f : 0f);
	Console.WriteLine($"Distance=15 → {result} (expected: St_Patrol)");

	// với sensor = 3 → không link nào thỏa
	result = StateEngine.Evaluate(
		CollectionsMarshal.AsSpan(idleLinks.Links ?? []),
		CollectionsMarshal.AsSpan(sg.States),
		idleRef,
		sensor => sensor.BeingType.Name == "S_DistanceSensor" ? 3f : 0f);
	Console.WriteLine($"Distance=3  → {result} (expected: St_Idle — no valid link)");
}
catch (Exception ex) { Console.WriteLine($"StateEngine error: {ex.Message}"); }

Console.WriteLine($"\nDone.");
