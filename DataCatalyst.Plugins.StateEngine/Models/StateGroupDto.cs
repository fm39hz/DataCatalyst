namespace DataCatalyst.Plugins.StateEngine.Models;

using System.Collections.Generic;
using Abstractions;
using DataCatalyst.Extensions.Composition;

/// <summary>Group of related states with shared configuration.</summary>
[DataComponent]
public readonly record struct StateGroup {
	/// <summary>Default multiplier for priority tier calculations.</summary>
	public const int DefaultTierScale = 10000;

	/// <summary>Default priority penalty per inheritance depth level.</summary>
	public const int DefaultDepthPenalty = 1000;

	/// <summary>Creates a StateGroup with default values.</summary>
	public StateGroup() {
		GroupId = string.Empty;
		RequiredTrait = string.Empty;
		DefaultState = string.Empty;
		States = [];
	}

	/// <summary>Unique identifier for the state group.</summary>
	public string GroupId { get; init; }

	/// <summary>Base priority tier for all states.</summary>
	public int PriorityTier { get; init; }

	/// <summary>Multiplier for priority tier calculations.</summary>
	public int TierScale { get; init; } = DefaultTierScale;

	/// <summary>Priority penalty per inheritance depth level.</summary>
	public int DepthPenalty { get; init; } = DefaultDepthPenalty;

	/// <summary>Trait required for this group to be active.</summary>
	public string RequiredTrait { get; init; }

	/// <summary>Fallback state when no transition matches.</summary>
	public string DefaultState { get; init; }

	/// <summary>All state definitions in this group.</summary>
	public Dictionary<string, StateDefinition> States { get; init; }
}

/// <summary>Defines a state with optional parent and transitions.</summary>
[DataComponent]
public readonly record struct StateDefinition {
	/// <summary>Parent state for hierarchical inheritance.</summary>
	public string? Parent { get; init; }

	/// <summary>Outgoing transitions from this state.</summary>
	public List<TransitionDef>? Transitions { get; init; }
}
