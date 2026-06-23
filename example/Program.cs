using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.Abstractions;
using DataCatalyst.Core;
using DataCatalyst.Loaders;
using DataCatalyst.Extensions.Materialization;
using DataCatalyst.Plugins.StateEngine.Models;
using DataCatalyst.Plugins.StateEngine.Core;
using Arch.Core;
using Arch.Core.Extensions;

// Setup environment
var env = new DataCatalystEnvironment();

Console.WriteLine("=== DataCatalyst Mini RPG & StateEngine AI Example ===\n");

// Paths setup
string FindFolder(string name) {
	var dir = AppContext.BaseDirectory;
	while (!string.IsNullOrEmpty(dir)) {
		var path = Path.Combine(dir, name);
		if (Directory.Exists(path)) return path;
		var parent = Directory.GetParent(dir);
		if (parent == null) break;
		dir = parent.FullName;
	}
	return Path.Combine(AppContext.BaseDirectory, name);
}

var dataPath = FindFolder("Data");
var dlcPath = FindFolder("DLC");
var modsPath = FindFolder("Mods");
var patchPath = FindFolder("Patch");

Console.WriteLine($"Base Data:     {dataPath}");
Console.WriteLine($"DLC Data:      {dlcPath}");
Console.WriteLine($"Mods Data:     {modsPath}");
Console.WriteLine($"Patch/Overlay: {patchPath}\n");

// Đăng ký thủ công các component StateEngine vào Registry vì nó nằm ở assembly khác không chạy SourceGen
PrimitiveRegistry.Default.Register<StateGroup>();
PrimitiveRegistry.Default.Register<StateDefinition>();
PrimitiveRegistry.Default.Register<DataCatalyst.Extensions.Composition.TransitionDef>();
PrimitiveRegistry.Default.Register<DataCatalyst.Extensions.Composition.ConditionGroupDef>();
PrimitiveRegistry.Default.Register<DataCatalyst.Extensions.Composition.SensorConditionDef>();
PrimitiveRegistry.Default.Register<DataCatalyst.Extensions.Composition.SensorInfluenceDef>();

PrimitiveRegistry.Default.RegisterIds(new Dictionary<string, Type> {
	{ "StateGroup", typeof(StateGroup) },
	{ "StateDefinition", typeof(StateDefinition) },
	{ "TransitionDef", typeof(DataCatalyst.Extensions.Composition.TransitionDef) },
	{ "ConditionGroupDef", typeof(DataCatalyst.Extensions.Composition.ConditionGroupDef) },
	{ "SensorConditionDef", typeof(DataCatalyst.Extensions.Composition.SensorConditionDef) },
	{ "SensorInfluenceDef", typeof(DataCatalyst.Extensions.Composition.SensorInfluenceDef) }
});

var loader = new JsonDataLoader(JsonDataLoader.DefaultOptions, env);

// Cấu hình Data Pipeline: Base -> DLC -> Mod -> Patch
var pipeline = new DataPipeline(env)
	.AddSource(new DataSource("Base", loader, dataPath) {
		Priority = 0,
		MergePolicy = MergePolicy.Patch
	})
	.AddSource(new DataSource("Expansion", loader, dlcPath) {
		Priority = 1,
		DependsOn = new[] { "Base" },
		MergePolicy = MergePolicy.Patch
	})
	.AddSource(new DataSource("SuperGoblinMod", loader, modsPath) {
		Priority = 2,
		DependsOn = new[] { "Expansion" },
		MergePolicy = MergePolicy.FieldPatch
	})
	.AddSource(new DataSource("LocalizationHotfix", loader, patchPath) {
		Priority = 3,
		MergePolicy = MergePolicy.Overlay
	});

var catalog = pipeline.Build();

if (pipeline.Diagnostics.Count > 0) {
	Console.WriteLine("Pipeline Diagnostics:");
	foreach (var diag in pipeline.Diagnostics) {
		Console.WriteLine($"  - {diag}");
	}
	Console.WriteLine();
}

Console.WriteLine($"Built catalog with {catalog.Entries.Count} entries.");

// -------------------------------------------------------------
// BAKE STATE MACHINE AI
// -------------------------------------------------------------
Console.WriteLine("\n--- Baking AI State Machine ---");
BakedStateGroup? bakedGuardAi = null;
List<string> stateNames = [];

if (catalog.Entries.TryGetValue("GuardAI", out var guardAiEntry) && guardAiEntry.TryGet<StateGroup>(out var stateGroup)) {
	bakedGuardAi = StateEngineBaker.Bake(stateGroup, catalog);
	stateNames = stateGroup.States.Keys.OrderBy(s => s).ToList();
	Console.WriteLine($"Baked State Machine '{bakedGuardAi.GroupId}' successfully.");
	Console.WriteLine($"States: {string.Join(", ", stateNames)} (Default: {stateGroup.DefaultState})");
	
	foreach (var (sId, state) in bakedGuardAi.States) {
		Console.WriteLine($"  [Baked State] ID: {sId} ({GetStateName(sId)}), Transitions: {state.Transitions.Length}");
		foreach (var t in state.Transitions) {
			Console.WriteLine($"    -> Target: {t.TargetStateId} ({GetStateName(t.TargetStateId)}), BasePriority: {t.BasePriority}");
		}
	}
} else {
	Console.WriteLine("Error: GuardAI not found in catalog!");
}

string GetStateName(int id) => id > 0 && id <= stateNames.Count ? stateNames[id - 1] : "Unknown";
int GetStateId(string name) => stateNames.IndexOf(name) + 1;

// -------------------------------------------------------------
// ARCH ECS INTEGRATION (AOT-SAFE)
// -------------------------------------------------------------
Console.WriteLine("\n--- Arch ECS Integration & Materialization ---");
var world = World.Create();

// Bảo toàn metadata của mảng component chống bị NativeAOT trim mất
var preserves = new object[] {
	new DataCatalyst.Generated.Health[1],
	new DataCatalyst.Generated.Damage[1],
	new DataCatalyst.Generated.ExperienceReward[1],
	new DataCatalyst.Generated.Amount[1],
	new DataCatalyst.Generated.Label[1],
	new DataCatalyst.Generated.Durability[1],
	new DataCatalyst.Generated.CurrentAIState[1]
};
System.GC.KeepAlive(preserves);

var mat = new DataMaterializer<EntityWrapper>();
mat.Register<DataCatalyst.Generated.Health>((w, val) => w.World.Add(w.Entity, val));
mat.Register<DataCatalyst.Generated.Damage>((w, val) => w.World.Add(w.Entity, val));
mat.Register<DataCatalyst.Generated.ExperienceReward>((w, val) => w.World.Add(w.Entity, val));
mat.Register<DataCatalyst.Generated.Amount>((w, val) => w.World.Add(w.Entity, val));
mat.Register<DataCatalyst.Generated.Label>((w, val) => w.World.Add(w.Entity, val));
mat.Register<DataCatalyst.Generated.Durability>((w, val) => w.World.Add(w.Entity, val));

// Duyệt qua catalog và sinh thực thể tương ứng trong Arch ECS
foreach (var entryKvp in catalog.Entries) {
	var key = entryKvp.Key;
	var entry = entryKvp.Value;
	
	// Skip sensors và state engines khỏi việc tạo thực thể vật lý trong ECS
	if (entry.TryGet<Concept>(out var concept) && concept.Value != null) {
		if (concept.Value.Contains("Sensor") || concept.Value.Contains("StateEngine")) continue;
	}

	var entity = world.Create();
	var wrapper = new EntityWrapper(entity, world);
	mat.Materialize(entry, wrapper);

	// Nếu thực thể là Enemy và có AI, đính kèm AI State ban đầu
	if (concept.Value != null && concept.Value.Contains("Enemy") && bakedGuardAi != null) {
		world.Add(entity, new DataCatalyst.Generated.CurrentAIState { StateId = bakedGuardAi.DefaultStateId });
	}
}

// In danh sách thực thể trong ECS
Console.WriteLine("ECS Entities Materialized:");
var allQuery = new QueryDescription().WithAll<DataCatalyst.Generated.Label>();
world.Query(in allQuery, (ref DataCatalyst.Generated.Label label) => {
	Console.WriteLine($"  - Entity: '{label.Value}'");
});

// -------------------------------------------------------------
// GAME LOOP TURN SIMULATION
// -------------------------------------------------------------
Console.WriteLine("\n--- RPG Game Turn & AI State Machine Simulation ---");

// Cấu hình mock sensor values
float distanceToPlayer = 15.0f;
float threatLevel = 0.0f;

// Viết delegate đọc sensor dựa trên tên tín hiệu của Catalog đã được bake sang Integer ID
float ReadSensor(int signalId) {
	var signalEntry = catalog.GetEntry(signalId);
	if (signalEntry == null) return 0.0f;
	
	float ret = 0.0f;
	if (signalEntry.Key == "PlayerDistance") ret = distanceToPlayer;
	else if (signalEntry.Key == "ThreatLevel") ret = threatLevel;
	
	Console.WriteLine($"      [Sensor Read] ID: {signalId}, Key: '{signalEntry.Key}', Value: {ret}");
	return ret;
}

var viableStates = new HashSet<int>(stateNames.Select(GetStateId));

void RunAITurn(int turnNumber) {
	Console.WriteLine($"\n[TURN {turnNumber}] (Sensors -> DistanceToPlayer: {distanceToPlayer}m, ThreatLevel: {threatLevel})");
	
	var aiQuery = new QueryDescription().WithAll<DataCatalyst.Generated.Label, DataCatalyst.Generated.CurrentAIState>();
	world.Query(in aiQuery, (ref DataCatalyst.Generated.Label label, ref DataCatalyst.Generated.CurrentAIState aiState) => {
		if (bakedGuardAi == null) return;

		var currentStateName = GetStateName(aiState.StateId);
		
		// Đánh giá chuyển trạng thái
		var evalResult = StateEngineEvaluator.Evaluate(
			currentStateId: aiState.StateId,
			group: bakedGuardAi,
			viableStates: viableStates,
			readSensor: ReadSensor
		);

		if (evalResult.HasValue && evalResult.TargetStateId != aiState.StateId) {
			var oldState = currentStateName;
			aiState.StateId = evalResult.TargetStateId;
			var newState = GetStateName(aiState.StateId);
			Console.WriteLine($"  * AI: Entity '{label.Value}' transitions [{oldState}] -> [{newState}]");
		} else {
			Console.WriteLine($"  * AI: Entity '{label.Value}' stays in [{currentStateName}]");
		}
	});
}

// Chạy turn 1: Player xa, AI tuần tra (Patrol)
RunAITurn(1);

// Chạy turn 2: Player lại gần (distance < 8), AI chuyển sang Attack
distanceToPlayer = 5.0f;
RunAITurn(2);

// Chạy turn 3: Player cast phép mạnh (threat > 8), AI chuyển sang Retreat
threatLevel = 9.0f;
RunAITurn(3);

// Chạy turn 4: Threat giảm (threat <= 2), Player chạy xa (distance > 12), AI chuyển về Patrol
distanceToPlayer = 14.0f;
threatLevel = 1.0f;
RunAITurn(4);

Console.WriteLine("\n=== Mini RPG Simulation Complete ===");

/// <summary>
/// AOT-safe wrapper to carry Entity and World context into materializers.
/// </summary>
public sealed class EntityWrapper {
	public Entity Entity { get; }
	public World World { get; }
	
	public EntityWrapper(Entity entity, World world) {
		Entity = entity;
		World = world;
	}
}

namespace DataCatalyst.Generated {
	[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
	internal partial class DataCatalystComponentsJsonContext : System.Text.Json.Serialization.JsonSerializerContext {}
}
