// Mod plugin: adds items + registers a Godot-side processing node.
// Compiled with the game, auto-registered via [ModuleInitializer].

using FM39hz.DataCatalyst.Runtime;
using Godot;

// Game-defined interface - exposed through ServiceRegistry
public interface IGodotSystemRegistry
{
	void AddChild(Node node);
	void AddProcess(IScriptSystem system);
}

public interface IScriptSystem
{
	void _Process(double delta);
	void _PhysicsProcess(double delta);
}

[ModPlugin("GodotItemVFX")]
public sealed class GodotItemVFX : IModPlugin
{
	public string Name => "GodotItemVFX";
	public string[] Dependencies => [];

	public void OnLoad(IModGameContext ctx)
	{
		// Inject data
		ItemMod.AddEntry("GlowingSword", new Item { Weight = 3f, Health = 80 });

		// Register Godot process logic
		var godot = ctx.GetService<IGodotSystemRegistry>();
		if (godot is not null)
		{
			godot.AddChild(new ItemVFXNode());
			godot.AddProcess(new ItemAuraProcessor());
		}
	}
}

// Godot node - spawned by the mod
public sealed class ItemVFXNode : Node
{
	public override void _Ready()
	{
		// ItemMod changes → GodotItemAdapter creates/removes nodes
	}
}

// Custom processing - runs every frame
public sealed class ItemAuraProcessor : IScriptSystem
{
	public void _Process(double delta)
	{
		foreach (var entry in ItemMod.GetAllModEntries())
		{
			// Animate based on entry data
		}
	}

	public void _PhysicsProcess(double delta) { }
}
