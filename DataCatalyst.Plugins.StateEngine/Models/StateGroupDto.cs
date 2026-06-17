namespace DataCatalyst.Plugins.StateEngine.Models;

using System.Collections.Generic;
using Abstractions;
using DataCatalyst.Plugins.Transition.Models;

/// <summary>Group of related states with shared configuration.</summary>
[DataComponent]
public sealed record StateGroup {
	/// <summary>Unique identifier for the state group.</summary>
	public string GroupId { get; init; } = "";

	/// <summary>Base priority tier for all states.</summary>
	public int PriorityTier { get; init; }

	/// <summary>Multiplier for priority tier calculations.</summary>
	public int TierScale { get; init; } = 10000;

	/// <summary>Priority penalty per inheritance depth level.</summary>
	public int DepthPenalty { get; init; } = 1000;

	/// <summary>Trait required for this group to be active.</summary>
	public string RequiredTrait { get; init; } = "";

	/// <summary>Fallback state when no transition matches.</summary>
	public string DefaultState { get; init; } = "";

	/// <summary>All state definitions in this group.</summary>
	public Dictionary<string, StateDefinition> States { get; init; } = [];
}

/// <summary>Defines a state with optional parent and transitions.</summary>
[DataComponent]
public sealed record StateDefinition {
	/// <summary>Parent state for hierarchical inheritance.</summary>
	public string? Parent { get; init; }

	/// <summary>Outgoing transitions from this state.</summary>
	public List<TransitionDef>? Transitions { get; init; }
}
