namespace DataCatalyst.Tests;

using DataCatalyst.Plugins.NumericCompare.Core;
using FluentAssertions;
using Plugins.NumericCompare.Contracts;
using Plugins.StateEngine.Core;
using Plugins.StateEngine.Models;
using Plugins.Transition.Models;
using Xunit;

public class StateEngineTests {
	[Theory]
	[InlineData("==", CompareOp.Equal)]
	[InlineData("eq", CompareOp.Equal)]
	[InlineData("!=", CompareOp.NotEqual)]
	[InlineData("neq", CompareOp.NotEqual)]
	[InlineData(">", CompareOp.GreaterThan)]
	[InlineData("gt", CompareOp.GreaterThan)]
	[InlineData(">=", CompareOp.GreaterThanOrEqual)]
	[InlineData("gte", CompareOp.GreaterThanOrEqual)]
	[InlineData("<", CompareOp.LessThan)]
	[InlineData("lt", CompareOp.LessThan)]
	[InlineData("<=", CompareOp.LessThanOrEqual)]
	[InlineData("lte", CompareOp.LessThanOrEqual)]
	public void OperatorParser_ParsesAllTokens(string token, CompareOp expected) =>
		OperatorParser.Parse(token).Should().Be(expected);

	[Fact]
	public void OperatorParser_ThrowsOnInvalidToken() {
		Action act = () => OperatorParser.Parse("invalid");
		act.Should().Throw<ArgumentException>();
	}

	[Theory]
	[InlineData(10f, CompareOp.Equal, 10f, true)]
	[InlineData(10.0001f, CompareOp.Equal, 10f, true)] // within default epsilon 0.001
	[InlineData(10.01f, CompareOp.Equal, 10f, false)]
	[InlineData(10.01f, CompareOp.NotEqual, 10f, true)]
	[InlineData(15f, CompareOp.GreaterThan, 10f, true)]
	[InlineData(10f, CompareOp.GreaterThan, 10f, false)]
	[InlineData(10f, CompareOp.GreaterThanOrEqual, 10f, true)]
	[InlineData(5f, CompareOp.LessThan, 10f, true)]
	[InlineData(10f, CompareOp.LessThan, 10f, false)]
	[InlineData(10f, CompareOp.LessThanOrEqual, 10f, true)]
	public void OperatorParser_EvaluateMatchesMath(float value, CompareOp op, float threshold, bool expected) =>
		OperatorParser.Evaluate(value, op, threshold).Should().Be(expected);

	private static string Fq(string groupId, string state) => $"{groupId}.{state}";

	[Fact]
	public void StateEngineEvaluator_NoState_ReturnsFalse() {
		var group = new StateGroup { GroupId = "AI" };
		var baked = StateEngineBaker.Bake(group, s => s, s => s);
		var result = StateEngineEvaluator<string, string>.Evaluate("AI.Idle", baked, ["AI.Idle"], s => 0f);
		result.HasValue.Should().BeFalse();
	}

	[Fact]
	public void StateEngineEvaluator_SimpleTransition_Succeeds() {
		var group = new StateGroup {
			GroupId = "GuardAI",
			PriorityTier = 1,
			DefaultState = "Idle",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Patrol",
							Priority = 5,
							Conditions = new ConditionGroupDef {
								All = [
									new SensorConditionDef { Signal = "time", Op = ">", Value = 5f }
								]
							}
						}
					]
				},
				["Patrol"] = new()
			}
		};

		var baked = StateEngineBaker.Bake(group, s => s, s => s);

		// 1. Time is 4.0 -> Should not transition
		var result1 = StateEngineEvaluator<string, string>.Evaluate(Fq("GuardAI", "Idle"), baked,
			[Fq("GuardAI", "Idle"), Fq("GuardAI", "Patrol")],
			s => s == "time" ? 4f : 0f);
		result1.HasValue.Should().BeFalse();

		// 2. Time is 6.0 -> Should transition to Patrol
		var result2 = StateEngineEvaluator<string, string>.Evaluate(Fq("GuardAI", "Idle"), baked,
			[Fq("GuardAI", "Idle"), Fq("GuardAI", "Patrol")],
			s => s == "time" ? 6f : 0f);
		result2.HasValue.Should().BeTrue();
		result2.TargetStateId.Should().Be(Fq("GuardAI", "Patrol"));
	}

	[Fact]
	public void StateEngineEvaluator_RespectsAllAnyNoneConditions() {
		var group = new StateGroup {
			GroupId = "GuardAI",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Patrol",
							Conditions = new ConditionGroupDef {
								All = [
									new SensorConditionDef { Signal = "has_target", Op = "==", Value = 0f }
								],
								Any = [
									new SensorConditionDef { Signal = "time", Op = ">", Value = 10f },
									new SensorConditionDef { Signal = "boredom", Op = ">", Value = 50f }
								],
								None = [
									new SensorConditionDef { Signal = "alert", Op = "==", Value = 1f }
								]
							}
						}
					]
				},
				["Patrol"] = new()
			}
		};

		var baked = StateEngineBaker.Bake(group, s => s, s => s);
		var idle = Fq("GuardAI", "Idle");
		var patrol = Fq("GuardAI", "Patrol");

		StateEngineEvaluator<string, string>.Result Run(float hasTarget, float time, float boredom, float alert) =>
			StateEngineEvaluator<string, string>.Evaluate(idle, baked, [patrol], s => s switch {
				"has_target" => hasTarget,
				"time" => time,
				"boredom" => boredom,
				"alert" => alert,
				_ => 0f
			});

		Run(0f, 2f, 60f, 0f).HasValue.Should().BeTrue();
		Run(1f, 15f, 60f, 0f).HasValue.Should().BeFalse();
		Run(0f, 2f, 10f, 0f).HasValue.Should().BeFalse();
		Run(0f, 15f, 60f, 1f).HasValue.Should().BeFalse();
	}

	[Fact]
	public void StateEngineEvaluator_HierarchicalTransitions_AppliesDepthPenalty() {
		var group = new StateGroup {
			GroupId = "RobotAI",
			PriorityTier = 1,
			TierScale = 10000,
			DepthPenalty = 1000,
			States = new Dictionary<string, StateDefinition> {
				["Patrol"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Refuel",
							Priority = 2000,
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
							Priority = 1500,
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

		var baked = StateEngineBaker.Bake(group, s => s, s => s);
		var result = StateEngineEvaluator<string, string>.Evaluate(
			Fq("RobotAI", "AggressivePatrol"), baked,
			[Fq("RobotAI", "Refuel"), Fq("RobotAI", "Attack")], s =>
			s switch {
				"battery" => 10f,
				"see_enemy" => 1f,
				_ => 0f
			});

		result.HasValue.Should().BeTrue();
		result.TargetStateId.Should().Be(Fq("RobotAI", "Attack"));
	}

	[Fact]
	public void StateEngineEvaluator_RespectsSensorInfluences() {
		var group = new StateGroup {
			GroupId = "CombatAI",
			PriorityTier = 1,
			TierScale = 1000,
			States = new Dictionary<string, StateDefinition> {
				["Search"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Attack",
							Priority = 500,
							Conditions = new ConditionGroupDef {
								All = [new SensorConditionDef { Signal = "in_range", Op = "==", Value = 1f }]
							},
							Influences = [
								new SensorInfluenceDef { Signal = "anger", Weight = 100f }
							]
						},
						new TransitionDef {
							TargetState = "Flee",
							Priority = 600,
							Conditions = new ConditionGroupDef {
								All = [new SensorConditionDef { Signal = "in_range", Op = "==", Value = 1f }]
							},
							Influences = [
								new SensorInfluenceDef { Signal = "fear", Weight = 50f }
							]
						}
					]
				},
				["Attack"] = new(),
				["Flee"] = new()
			}
		};

		var baked = StateEngineBaker.Bake(group, s => s, s => s);
		var search = Fq("CombatAI", "Search");
		var viable = new HashSet<string>([Fq("CombatAI", "Attack"), Fq("CombatAI", "Flee")]);

		// Scenario 1: Anger is low (1.0), Fear is high (5.0)
		var result1 = StateEngineEvaluator<string, string>.Evaluate(search, baked, viable, s =>
			s switch { "in_range" => 1f, "anger" => 1f, "fear" => 5f, _ => 0f });
		result1.TargetStateId.Should().Be(Fq("CombatAI", "Flee"));

		// Scenario 2: Anger is high (6.0), Fear is low (1.0)
		var result2 = StateEngineEvaluator<string, string>.Evaluate(search, baked, viable, s =>
			s switch { "in_range" => 1f, "anger" => 6f, "fear" => 1f, _ => 0f });
		result2.TargetStateId.Should().Be(Fq("CombatAI", "Attack"));
	}

	[Fact]
	public void StateEngineEvaluator_ExitValue_AppliesHysteresis() {
		var group = new StateGroup {
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

		var baked = StateEngineBaker.Bake(group, s => s, s => s);
		var active = Fq("TempAI", "Active");

		// 1. Current state is Idle (not at target Active). Must use Value (50f).
		var resultIdle = StateEngineEvaluator<string, string>.Evaluate(
			Fq("TempAI", "Idle"), baked, [active], s => s == "temp" ? 45f : 0f);
		resultIdle.HasValue.Should().BeFalse();

		// 2. Current state is Active (at target Active). Use ExitValue (40f).
		var resultActive1 = StateEngineEvaluator<string, string>.Evaluate(
			active, baked, [active], s => s == "temp" ? 45f : 0f);
		resultActive1.HasValue.Should().BeTrue();
		resultActive1.TargetStateId.Should().Be(active);

		// 3. Current state is Active. Temp is 35f -> should exit Active (35 > 40 is false)
		var resultActive2 = StateEngineEvaluator<string, string>.Evaluate(
			active, baked, [active], s => s == "temp" ? 35f : 0f);
		resultActive2.HasValue.Should().BeFalse();
	}

	[Fact]
	public void StateEngineEvaluator_HierarchicalTransitions_FallsBackToParent() {
		var group = new StateGroup {
			GroupId = "RobotAI",
			PriorityTier = 1,
			TierScale = 10000,
			DepthPenalty = 1000,
			States = new Dictionary<string, StateDefinition> {
				["Patrol"] = new() {
					Transitions = [
						new TransitionDef {
							TargetState = "Refuel",
							Priority = 2000,
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
							Priority = 1500,
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

		var baked = StateEngineBaker.Bake(group, s => s, s => s);
		var result = StateEngineEvaluator<string, string>.Evaluate(
			Fq("RobotAI", "AggressivePatrol"), baked,
			[Fq("RobotAI", "Refuel"), Fq("RobotAI", "Attack")], s =>
			s switch { "battery" => 10f, "see_enemy" => 0f, _ => 0f });

		result.HasValue.Should().BeTrue();
		result.TargetStateId.Should().Be(Fq("RobotAI", "Refuel"));
	}
}
