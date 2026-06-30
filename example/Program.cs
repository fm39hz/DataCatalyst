using System;
using System.IO;
using System.Runtime.InteropServices;
using Catalyst;
using Catalyst.Loaders;
using Catalyst.Pipeline;
using Catalyst.Registry;
using Catalyst.Generated;
using Catalyst.StateEngine;

var root = AppContext.BaseDirectory;
while (root != null && !Directory.Exists(Path.Combine(root, "Data")))
	root = Directory.GetParent(root)?.FullName!;
if (string.IsNullOrEmpty(root))
	root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "example");

var registries = new RegistrySet();
CatalystRegistries.Populate(registries);

var knowledge = new Pipeline(registries)
	.AddSource("Base", new JsonDataLoader(registries.Beings), Path.Combine(root, "Data"))
	.AddSource("DLC", new JsonDataLoader(registries.Beings), Path.Combine(root, "DLC"))
	.AddSource("Mods", new JsonDataLoader(registries.Beings), Path.Combine(root, "Mods"))
	.Build(out var diag);

foreach (var item in diag.Items)
	Console.Error.WriteLine($"  {item}");

// Query RoboticKnight via concept-revealed aspects
Console.WriteLine($"=== Knowledge ===");
var rig = knowledge.Of<Humanoid, SkeletonRig>(typeof(RoboticKnight));
Console.WriteLine($"RoboticKnight — Bones: {rig.BoneCount}, Bipedal: {rig.IsBipedal}");
var power = knowledge.Of<Mechanical, BatteryCapacity>(typeof(RoboticKnight));
Console.WriteLine($"RoboticKnight — Power: {power.MaxJoules} J, Efficiency: {power.Efficiency:P}");

// State Engine — FSM từ ABC primitives
Console.WriteLine($"\n=== StateEngine ===");
var sg = knowledge.Of<State, StateGroup>(typeof(RoboticKnight));
Console.WriteLine($"Default state: {sg.DefaultState}");
foreach (var s in sg.States)
{
	var links = StateEngine.GetLinks(knowledge, s);
	var des = StateEngine.GetDesirability(knowledge, s);
	Console.WriteLine($"  {s}: {links.Links?.Count ?? 0} link(s) [priority={des.Priority}]");
}

// Evaluate: St_Idle với sensor = 15 → chuyển sang St_Patrol
var idleRef = new Ref<State>(typeof(St_Idle));
var idleLinks = StateEngine.GetLinks(knowledge, idleRef);

var result = StateEngine.Evaluate(
	CollectionsMarshal.AsSpan(idleLinks.Links ?? []),
	CollectionsMarshal.AsSpan(sg.States),
	idleRef,
	sensor => sensor.BeingType.Name == "S_DistanceSensor" ? 15f : 0f);
Console.WriteLine($"\nSensor=15 → {result} (expected: St_Patrol)");

result = StateEngine.Evaluate(
	CollectionsMarshal.AsSpan(idleLinks.Links ?? []),
	CollectionsMarshal.AsSpan(sg.States),
	idleRef,
	sensor => sensor.BeingType.Name == "S_DistanceSensor" ? 3f : 0f);
Console.WriteLine($"Sensor=3  → {result} (expected: St_Idle — no valid link)");

Console.WriteLine($"\nDone.");
