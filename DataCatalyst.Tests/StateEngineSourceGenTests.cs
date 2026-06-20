namespace DataCatalyst.Tests;

using System.Collections.Generic;
using DataCatalyst.Core;
using DataCatalyst.Extensions.Composition;
using DataCatalyst.Plugins.StateEngine.Contracts;
using DataCatalyst.Plugins.StateEngine.Core;
using DataCatalyst.Plugins.StateEngine.Models;
using FluentAssertions;
using Xunit;

// SourceGen auto-generates mappers for these enums via [StateEnum] and [SensorEnum]
[StateEnum]
public enum PlayerState { Idle, Run, Jump, Attack, Patrol }

[SensorEnum]
public enum PlayerSensor { Speed, IsGrounded, Health, Alert }

public class StateEngineSourceGenTests {
	[Fact]
	public void SourceGen_AutoRegisters_StateMapper() {
		var mapper = MapperRegistry.Default.Get<IStateMapper<PlayerState>>();
		mapper.Should().NotBeNull();

		var idle = mapper!.MapState("PlayerState.Idle", "PlayerState");
		idle.Should().Be(PlayerState.Idle);
	}

	[Fact]
	public void SourceGen_AutoRegisters_SensorMapper() {
		var mapper = MapperRegistry.Default.Get<ISensorMapper<PlayerSensor>>();
		mapper.Should().NotBeNull();

		var speed = mapper!.MapSensor("Speed");
		speed.Should().Be(PlayerSensor.Speed);
	}

	[Fact]
	public void Bake_WithSourceGenMapper_WorksCorrectly() {
		// Arrange
		var rawGroup = new StateGroup {
			GroupId = "PlayerState",
			DefaultState = "Idle",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Run",
							Priority = 5,
							Conditions = new ConditionGroupDef {
								All = [
									new SensorConditionDef { Signal = "Speed", Op = ">", Value = 0.1f }
								]
							}
						}
					]
				},
				["Run"] = new()
			}
		};

		// Act — uses auto-registered mappers from SourceGen
		var baked = StateEngineBaker.Bake<PlayerState, PlayerSensor>(rawGroup);

		// Assert
		baked.DefaultState.Should().Be(PlayerState.Idle);
		baked.States.Should().ContainKey(PlayerState.Idle);

		var idleState = baked.States[PlayerState.Idle];
		idleState.Transitions.Length.Should().Be(1);
		idleState.Transitions[0].TargetState.Should().Be(PlayerState.Run);
	}

	[Fact]
	public void Evaluate_WithSourceGenMapper_WorksCorrectly() {
		// Arrange
		var rawGroup = new StateGroup {
			GroupId = "PlayerState",
			DefaultState = "Idle",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Run",
							Priority = 5,
							Conditions = new ConditionGroupDef {
								All = [
									new SensorConditionDef { Signal = "Speed", Op = ">", Value = 0.1f }
								]
							}
						}
					]
				},
				["Run"] = new()
			}
		};

		var baked = StateEngineBaker.Bake<PlayerState, PlayerSensor>(rawGroup);
		var viable = new HashSet<PlayerState> { PlayerState.Run };

		// Act — slow speed, should not transition
		var resultSlow = StateEngineEvaluator<PlayerState, PlayerSensor>.Evaluate(
			PlayerState.Idle,
			baked,
			viable,
			sensor => sensor == PlayerSensor.Speed ? 0f : 0f);

		resultSlow.HasValue.Should().BeFalse();

		// Act — fast speed, should transition to Run
		var resultFast = StateEngineEvaluator<PlayerState, PlayerSensor>.Evaluate(
			PlayerState.Idle,
			baked,
			viable,
			sensor => sensor == PlayerSensor.Speed ? 1f : 0f);

		resultFast.HasValue.Should().BeTrue();
		resultFast.TargetStateId.Should().Be(PlayerState.Run);
	}
}
