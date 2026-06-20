namespace DataCatalyst.Tests;

using System;
using System.Collections.Generic;
using DataCatalyst.Extensions.Composition;
using DataCatalyst.Plugins.StateEngine.Core;
using DataCatalyst.Plugins.StateEngine.Models;
using FluentAssertions;
using Xunit;

public enum GameStateKind : ulong {
	Idle = 1,
	Patrol = 2,
	Attack = 3,
	Refuel = 4
}

public enum GameSensorKind {
	Battery = 1,
	SeeEnemy = 2,
	Anger = 3,
	Fear = 4
}

public class StateEngineGenericTests {
	[Fact]
	public void GenericStateEngine_BakingAndEvaluation_Succeeds() {
		// Arrange: Create a raw hierarchical string-based StateGroup
		var rawGroup = new StateGroup {
			GroupId = "RobotAI",
			PriorityTier = 1,
			TierScale = 10000,
			DepthPenalty = 1000,
			DefaultState = "Patrol",
			States = new Dictionary<string, StateDefinition> {
				["Patrol"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Refuel",
							Priority = 2000, // Parent transition priority
							Conditions = new ConditionGroupDef {
								All = [new SensorConditionDef { Signal = "battery", Op = "<", Value = 20f }]
							}
						}
					]
				},
				["AggressivePatrol"] = new() {
					Parent = "Patrol",
					Transitions = [
						new TransitionDef {
							TargetState = "Attack",
							Priority = 1500, // Child transition priority
							Conditions = new ConditionGroupDef {
								All = [new SensorConditionDef { Signal = "see_enemy", Op = "==", Value = 1f }]
							}
						}
					]
				},
				["Refuel"] = new(),
				["Attack"] = new()
			}
		};

		// Define state mapping (e.g. mapping string IDs to GameStateKind enums)
		GameStateKind stateMapper(string s) => s switch {
			"RobotAI.Patrol" => GameStateKind.Patrol,
			"RobotAI.AggressivePatrol" => GameStateKind.Patrol, // Map AggressivePatrol to Patrol for the test
			"RobotAI.Refuel" => GameStateKind.Refuel,
			"RobotAI.Attack" => GameStateKind.Attack,
			_ => throw new ArgumentException($"Unknown state: {s}")
		};

		// Define sensor mapping
		GameSensorKind sensorMapper(string s) => s switch {
			"battery" => GameSensorKind.Battery,
			"see_enemy" => GameSensorKind.SeeEnemy,
			"anger" => GameSensorKind.Anger,
			"fear" => GameSensorKind.Fear,
			_ => throw new ArgumentException($"Unknown sensor: {s}")
		};

		// Act: Bake the group
		var baked = StateEngineBaker.Bake(rawGroup, stateMapper, sensorMapper);

		// Assert Baked structure
		baked.GroupId.Should().Be("RobotAI");
		baked.DefaultState.Should().Be(GameStateKind.Patrol);
		baked.States.Should().ContainKey(GameStateKind.Patrol);

		// Patrol (which represents AggressivePatrol here) should have 2 transitions:
		// 1. Attack (from child AggressivePatrol) - base priority: 10000 + 1500 = 11500
		// 2. Refuel (from parent Patrol) - base priority: 10000 + 2000 - 1000 = 11000
		var bakedPatrolState = baked.States[GameStateKind.Patrol];
		bakedPatrolState.Transitions.Length.Should().Be(2);

		var tAttack = bakedPatrolState.Transitions[0];
		tAttack.TargetState.Should().Be(GameStateKind.Attack);
		tAttack.BasePriority.Should().Be(11500);

		var tRefuel = bakedPatrolState.Transitions[1];
		tRefuel.TargetState.Should().Be(GameStateKind.Refuel);
		tRefuel.BasePriority.Should().Be(11000);

		// Act: Evaluate
		// Case 1: Low battery (10), see enemy (1)
		// Attack priority: 11500
		// Refuel priority: 11000
		// Attack should win because of higher baked priority
		var viable = new HashSet<GameStateKind> { GameStateKind.Refuel, GameStateKind.Attack };
		var result1 = StateEngineEvaluator<GameStateKind, GameSensorKind>.Evaluate(
			GameStateKind.Patrol,
			baked,
			viable,
			sensor => sensor switch {
				GameSensorKind.Battery => 10f,
				GameSensorKind.SeeEnemy => 1f,
				GameSensorKind.Anger => throw new NotImplementedException(),
				GameSensorKind.Fear => throw new NotImplementedException(),
				_ => 0f
			});

		result1.HasValue.Should().BeTrue();
		result1.TargetStateId.Should().Be(GameStateKind.Attack);

		// Case 2: Low battery (10), NO enemy seen (0)
		// Attack condition fails, should fall back to Refuel
		var result2 = StateEngineEvaluator<GameStateKind, GameSensorKind>.Evaluate(
			GameStateKind.Patrol,
			baked,
			viable,
			sensor => sensor switch {
				GameSensorKind.Battery => 10f,
				GameSensorKind.SeeEnemy => 0f,
				GameSensorKind.Anger => throw new NotImplementedException(),
				GameSensorKind.Fear => throw new NotImplementedException(),
				_ => 0f
			});

		result2.HasValue.Should().BeTrue();
		result2.TargetStateId.Should().Be(GameStateKind.Refuel);
	}

	[Fact]
	public void GenericStateEngine_ExitValue_AppliesHysteresis() {
		// Arrange
		var rawGroup = new StateGroup {
			GroupId = "TempAI",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Active",
							Conditions = new ConditionGroupDef {
								All = [
									new SensorConditionDef { Signal = "temp", Op = ">", Value = 50f, ExitValue = 40f }
								]
							}
						}
					]
				},
				["Active"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Active",
							Conditions = new ConditionGroupDef {
								All = [
									new SensorConditionDef { Signal = "temp", Op = ">", Value = 50f, ExitValue = 40f }
								]
							}
						}
					]
				}
			}
		};

		var baked = StateEngineBaker.Bake(
			rawGroup,
			s => s,
			s => s);

		var viable = new HashSet<string> { "TempAI.Active" };

		// 1. Current is Idle (not target Active). Value threshold is 50.
		// Temp is 45 -> should not transition.
		var res1 = StateEngineEvaluator<string, string>.Evaluate(
			"TempAI.Idle",
			baked,
			viable,
			s => s == "temp" ? 45f : 0f);
		res1.HasValue.Should().BeFalse();

		// 2. Current is Active (target Active). ExitValue threshold is 40.
		// Temp is 45 -> should stay Active.
		var res2 = StateEngineEvaluator<string, string>.Evaluate(
			"TempAI.Active",
			baked,
			viable,
			s => s == "temp" ? 45f : 0f);
		res2.HasValue.Should().BeTrue();
		res2.TargetStateId.Should().Be("TempAI.Active");

		// 3. Current is Active. Temp is 35 -> should exit Active.
		var res3 = StateEngineEvaluator<string, string>.Evaluate(
			"TempAI.Active",
			baked,
			viable,
			s => s == "temp" ? 35f : 0f);
		res3.HasValue.Should().BeFalse();
	}
}
