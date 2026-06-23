namespace DataCatalyst.Tests;

using DataCatalyst.Abstractions;

using System.Collections.Generic;
using DataCatalyst.Core;
using DataCatalyst.Extensions.Composition;
using DataCatalyst.Plugins.StateEngine.Core;
using DataCatalyst.Plugins.StateEngine.Models;
using FluentAssertions;
using Xunit;

public class StateEngineTests {
	[Fact]
	public void Bake_ProducesIntBasedStateGroup() {
		var rawGroup = new StateGroup {
			GroupId = "Locomotion",
			DefaultState = "Idle",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Patrol",
							Priority = 5,
							Conditions = new ConditionGroupDef {
								All = [
									new SensorConditionDef { Signal = "Speed", Op = ">", Value = 0.1f }
								]
							}
						}
					]
				},
				["Patrol"] = new()
			}
		};

		var speedEntry = new DataEntry("Speed");
		var graph = DataGraphBuilder.Build([speedEntry]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		var baked = StateEngineBaker.Bake(rawGroup, catalog);

		baked.GroupId.Should().Be("Locomotion");
		baked.DefaultStateId.Should().Be(1);
		baked.States.Should().ContainKey(1);

		var idleState = baked.States[1];
		idleState.Transitions.Length.Should().Be(1);
		idleState.Transitions[0].TargetStateId.Should().Be(2);
	}

	[Fact]
	public void Evaluate_TransitionsWhenSensorExceedsThreshold() {
		var rawGroup = new StateGroup {
			GroupId = "Locomotion",
			DefaultState = "Idle",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Patrol",
							Priority = 5,
							Conditions = new ConditionGroupDef {
								All = [
									new SensorConditionDef { Signal = "Speed", Op = ">", Value = 0.1f }
								]
							}
						}
					]
				},
				["Patrol"] = new()
			}
		};

		var speedEntry = new DataEntry("Speed");
		var graph = DataGraphBuilder.Build([speedEntry]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		var baked = StateEngineBaker.Bake(rawGroup, catalog);
		var viable = new HashSet<int> { 2 };

		var speedId = catalog.GetEntryId("Speed");

		var resultSlow = StateEngineEvaluator.Evaluate(
			1, baked, viable, signal => signal == speedId ? 0f : 0f);
		resultSlow.HasValue.Should().BeFalse();

		var resultFast = StateEngineEvaluator.Evaluate(
			1, baked, viable, signal => signal == speedId ? 1f : 0f);
		resultFast.HasValue.Should().BeTrue();
		resultFast.TargetStateId.Should().Be(2);
	}

	[Fact]
	public void Evaluate_DefaultState_WhenNoTransitionMatches() {
		var rawGroup = new StateGroup {
			GroupId = "Locomotion",
			DefaultState = "Idle",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Patrol",
							Priority = 5,
							Conditions = new ConditionGroupDef {
								All = [
									new SensorConditionDef { Signal = "Speed", Op = ">", Value = 10f }
								]
							}
						}
					]
				},
				["Patrol"] = new()
			}
		};

		var speedEntry = new DataEntry("Speed");
		var graph = DataGraphBuilder.Build([speedEntry]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		var baked = StateEngineBaker.Bake(rawGroup, catalog);
		var viable = new HashSet<int> { 2 };

		var result = StateEngineEvaluator.Evaluate(
			1, baked, viable, signal => 0f);
		result.HasValue.Should().BeFalse();
	}
}
