# DataCatalyst Architecture & Usage Guide

## Triết lý

**DataCatalyst** là compile-time composition framework. Data là Single Source of Truth.

- Data → types (SourceGen) → runtime access (DataCatalog)
- Loader format-agnostic (JSON, CSV, MessagePack, ...)
- Core/Plugin không biết format — chỉ biết `Get<T>()`
- Concept là first-class: mọi entry phải thuộc ít nhất 1 concept
- DC là immutable: readonly sau Resolve. Không query runtime.

---

## Core API

```csharp
// Pipeline
var env    = new DataCatalystEnvironment();
env.Plugins.Register<GameConceptPlugin>();

var loaded = JsonDataLoader.LoadDirectory("Data/", jsonOptions, env);
var graph  = DataGraphBuilder.Build(loaded.Entries, env: env);
var catalog = DataCatalogBuilder.Resolve(graph, env: env);

// Access
catalog.Get<Health>(Concept.Enemy.Goblin).Current       // fast path
catalog.Entries["Goblin"].Get<Health>().Current          // string key
catalog.TryGet<Element>(Concept.Enemy.FireDragon, out var e) // optional

// Concept-scoped
var enemies = catalog.GetConcept<Concept.Enemy>();
enemies.Get<Health>(Concept.Enemy.Goblin).Current
```

---

## Deeply Nested Data

SourceGen generates structs per nesting level. Inheritance merges automatically.

```json
{
    "Concept": "Enemy",
    "BaseEnemy": true,
    "Stats": {
        "Vitality": { "Base": 500, "Growth": 50 },
        "Mana":     { "Base": 5, "Growth": 1 }
    },
    "Damage": {
        "Breath": { "Type": "Fire", "Min": 30, "Max": 50, "Cooldown": 5.0 },
        "Claw":   { "Min": 15, "Max": 25, "Cooldown": 1.5 }
    },
    "Defense": {
        "Armor": 20,
        "Resistance": { "Fire": 100, "Ice": -50, "Lightning": 0 }
    }
}
```

```csharp
// SourceGen generates:
struct StatsVitality     { int Base; int Growth; }
struct StatsMana         { int Base; int Growth; }
struct Stats             { StatsVitality Vitality; StatsMana Mana; }
struct DamageBreath      { string Type; int Min; int Max; float Cooldown; }
struct DamageClaw        { int Min; int Max; float Cooldown; }
struct Damage            { DamageBreath Breath; DamageClaw Claw; }
struct DefenseResistance { int Fire; int Ice; int Lightning; }
struct Defense           { int Armor; DefenseResistance Resistance; }

// Access at any level
catalog.Get<DamageBreath>(Concept.Enemy.FireDragon).Type    // "Fire"
catalog.Get<StatsVitality>(Concept.Enemy.Dragon).Base        // 500
```

---

## Materializer

Bridge giữa DC component → engine-specific object.

```csharp
var mat = new DataMaterializer<GameObject>();
mat.Register<Health>((go, h) => {
    var bar = go.GetComponent<HealthBar>();
    bar.Max = h.Max; bar.Current = h.Current;
});
mat.Register<DamageBreath>((go, d) =>
    go.GetComponent<BreathWeapon>().Init(d.Type, d.Min, d.Max, d.Cooldown));

// Dùng khi spawn entity
var goblin = Instantiate(prefab);
mat.Materialize(catalog.Entries["Goblin"], goblin);
```

Eager: Materialize tất cả entries sau Resolve.
Lazy: Materialize từng entry khi spawn.

---

## StateEngine — Centralized AI

StateEngine được thiết kế cho ECS: **1 system duy nhất xử lý tất cả entities**.

```csharp
// Bake 1 lần, dùng chung cho mọi entities cùng loại
var baked = StateEngineBaker.Bake(
    catalog.Get<StateGroup>(Concept.CombatAI.CombatAI), catalog);

// 1 system, 1 query, evaluate tất cả
public struct AISystem : ISystem {
    BakedStateGroup baked;

    public void OnUpdate(ref SystemState state) {
        foreach (var ai in SystemAPI.Query<RefRW<AIStateECS>>()) {
            var result = StateEngineEvaluator.Evaluate(
                ai.ValueRW.CurrentState, baked, null,
                signalId => ReadSensor(signalId));
            if (result.HasValue)
                ai.ValueRW.CurrentState = result.TargetStateId;
        }
    }
}
```

### Data format

```json
{
    "Concept": "CombatAI",
    "DefaultState": "Idle",
    "States": {
        "Idle": {
            "Transitions": [{
                "TargetState": "Chase",
                "Priority": 5,
                "Conditions": {
                    "All": [{ "Signal": "PlayerDistance", "Op": "<", "Value": 50 }]
                }
            }]
        },
        "Chase": {
            "Transitions": [
                { "TargetState": "Attack", "Priority": 10,
                  "Conditions": { "All": [{ "Signal": "PlayerDistance", "Op": "<", "Value": 5 }] } },
                { "TargetState": "Idle", "Priority": 1,
                  "Conditions": { "All": [{ "Signal": "PlayerDistance", "Op": ">=", "Value": 50 }] } }
            ]
        },
        "Attack": {}
    }
}
```

---

## Engine Integration

### Unity — MonoBehaviour + Materializer

```csharp
void Awake() {
    var loaded = JsonDataLoader.LoadDirectory("Data", options, env);
    var graph  = DataGraphBuilder.Build(loaded.Entries, env: env);
    Catalog    = DataCatalogBuilder.Resolve(graph, env: env);
    DragonAI   = StateEngineBaker.Bake(
        Catalog.Get<StateGroup>(Concept.CombatAI.CombatAI), Catalog);

    mat = new DataMaterializer<GameObject>();
    mat.Register<Health>((go, h) => /* assign */);
    mat.Register<DamageBreath>((go, d) => /* assign */);
}

void SpawnDragon(string key) {
    var go = Instantiate(prefab);
    mat.Materialize(Catalog.Entries[key], go);
    go.GetComponent<DragonState>().baked = DragonAI;
}
```

### Unity — DOTS

```csharp
[BurstCompile]
public partial struct AISystem : ISystem {
    BakedStateGroup baked;

    public void OnUpdate(ref SystemState state) {
        foreach (var ai in SystemAPI.Query<RefRW<AIStateECS>>()) {
            var result = StateEngineEvaluator.Evaluate(
                ai.ValueRW.CurrentState, baked, null, ReadSensor);
            if (result.HasValue)
                ai.ValueRW.CurrentState = result.TargetStateId;
        }
    }
}
```

### Godot — Node

```csharp
public override void _Ready() {
    var json = JsonDataLoader.LoadDirectory("res://Data", options, env);
    var graph = DataGraphBuilder.Build(json.Entries, env: env);
    Catalog = DataCatalogBuilder.Resolve(graph, env: env);
    DragonAI = StateEngineBaker.Bake(
        Catalog.Get<StateGroup>(Concept.CombatAI.CombatAI), Catalog);

    EnemyMat = new DataMaterializer<CharacterBody3D>();
    EnemyMat.Register<Health>((node, h) =>
        node.GetNode<HealthBar>("HealthBar").MaxValue = h.Current);
}
```

### Godot — Friflo ECS

```csharp
public override void _Process(double delta) {
    var query = store.Query<AIStateECS, BreathTimerECS>();
    query.ForEachEntity((ref AIStateECS ai, ref BreathTimerECS bt) => {
        var result = StateEngineEvaluator.Evaluate(
            ai.CurrentState, baked, null,
            signalId => signalId switch {
                0 => playerDist,
                1 => bt.Remaining,
                _ => 0f
            });
        if (result.HasValue) ai.CurrentState = result.TargetStateId;
    });
}
```

### MonoGame — Game Component

```csharp
protected override void LoadContent() {
    var json = JsonDataLoader.LoadDirectory("Content/Data", options, env);
    var graph = DataGraphBuilder.Build(json.Entries, env: env);
    catalog = DataCatalogBuilder.Resolve(graph, env: env);
    bakedAI = StateEngineBaker.Bake(
        catalog.Get<StateGroup>(Concept.CombatAI.CombatAI), catalog);

    mat = new DataMaterializer<DragonEntity>();
    mat.Register<Health>((e, h) => { e.HP = h.Current; e.MaxHP = h.Max; });
    foreach (var key in new[] { "FireDragon", "IceDragon", "Dragon" })
        mat.Materialize(catalog.Entries[key], dragons.Add(new()));
}
```

### MonoGame — Friflo ECS

```csharp
public void Update(float playerDistance, float delta) {
    store.Query<AIStateECS, BreathTimerECS>().ForEachEntity(
        (ref AIStateECS ai, ref BreathTimerECS bt) => {
            var result = StateEngineEvaluator.Evaluate(
                ai.CurrentState, baked, null,
                signalId => signalId switch {
                    0 => playerDistance, 1 => bt.Remaining, _ => 0f
                });
            if (result.HasValue) ai.CurrentState = result.TargetStateId;
        });
}
```

---

## Pattern Summary

| Pattern | DC handles | Consumer code |
|---------|-----------|---------------|
| Deep nested data | Struct per nesting level | `Get<DamageBreath>(id).Type` |
| Inheritance | ComponentMerger merge parent→child | Invisible — same API |
| Multi-concept | Entry thuộc N concepts | Access via any `Concept.X.Y` |
| Materializer | Register mapping → auto-apply | `mat.Register<T>((obj, c) => ...)` |
| StateEngine | One baked group cho tất cả | 1 system, evaluate tất cả |
| AISensor | Sensor = entry "AISensor" concept | `readSensor` lambda, signalId = entry ID |
| Immutable | Read-only sau Resolve | Không mutation API |
