// Mod plugin: adds new items + registers an ECS system
// Compiled together with the game, auto-registered via [ModuleInitializer].

using FM39hz.DataCatalyst.Runtime;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

// Game-defined interface — exposed through ServiceRegistry
public interface ISystemRegistry {
    void Register<T>(T system) where T : QuerySystem;
    void RegisterAfter<T, TAfter>(T system)
        where T : QuerySystem where TAfter : QuerySystem;
}

[ModPlugin("ItemPack")]
public sealed class ItemPack : IModPlugin {
    public string Name => "ItemPack";
    public string[] Dependencies => [];

    public void OnLoad(IModGameContext ctx) {
        // Inject new items via DataCatalyst
        ItemMod.AddRange(
            ("ThunderSword", new Item { Weight = 4f, Health = 0 }),
            ("LifeRing",     new Item { Weight = 0.2f, Health = 50 })
        );

        // Register Friflo system into physics pipeline
        var systems = ctx.GetService<ISystemRegistry>();
        if (systems is not null) {
            systems.RegisterAfter<ItemAuraSystem, CollisionSystem>();
        }
    }
}

// Friflo ECS system — processes items added by the mod
public sealed class ItemAuraSystem : QuerySystem {
    [Query]
    public void Run(ref ItemKindRef kind, ref ItemWeight weight) {
        // e.g. heavy items emit an aura
    }
}

// ECS components for item data
public struct ItemWeight : IComponent { public float Value; }
public struct ItemHealth : IComponent { public int Value; }
public struct ItemKindRef : IComponent { public ItemKind Kind; }
