namespace DataCatalyst.Tests; 
using DataCatalyst.Plugins.NumericCompare.Contracts;
using DataCatalyst.Plugins.NumericCompare.Core;
using DataCatalyst.Plugins.StateMachine.Core;
using DataCatalyst.Plugins.StateMachine.Models;
using DataCatalyst.Plugins.Transition.Models;
using FluentAssertions;
using Xunit;

public class StateMachineTests {
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
	public void OperatorParser_ParsesAllTokens(string token, CompareOp expected) => OperatorParser.Parse(token).Should().Be(expected);

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
	public void OperatorParser_EvaluateMatchesMath(float value, CompareOp op, float threshold, bool expected) => OperatorParser.Evaluate(value, op, threshold).Should().Be(expected);

	[Fact]
	public void StateMachineEvaluator_NoState_ReturnsFalse() {
		var group = new StateGroup { GroupId = "AI" };
		var result = StateMachineEvaluator.Evaluate("Idle", group, ["AI.Idle"], s => 0f);
		result.HasValue.Should().BeFalse();
	}

	[Fact]
	public void StateMachineEvaluator_SimpleTransition_Succeeds() {
		// Arrange
		var group = new StateGroup {
			GroupId = "GuardAI",
			PriorityTier = 1,
			DefaultState = "Idle",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new StateDefinition {
					Transitions = [
					new() {
						TargetState = "Patrol",
						Priority = 5,
						Conditions = new ConditionGroupDef {
							All = [
								new() { Signal = "time", Op = ">", Value = 5f }
							]
						}
					}
				]
				},
				["Patrol"] = new StateDefinition()
			}
		};

		// 1. Time is 4.0 -> Should not transition
		var result1 = StateMachineEvaluator.Evaluate("Idle", group, ["GuardAI.Idle", "GuardAI.Patrol"], s => s == "time" ? 4f : 0f);
		result1.HasValue.Should().BeFalse();

		// 2. Time is 6.0 -> Should transition to Patrol
		var result2 = StateMachineEvaluator.Evaluate("Idle", group, ["GuardAI.Idle", "GuardAI.Patrol"], s => s == "time" ? 6f : 0f);
		result2.HasValue.Should().BeTrue();
		result2.TargetStateId.Should().Be("GuardAI.Patrol");
	}

	[Fact]
	public void StateMachineEvaluator_RespectsAllAnyNoneConditions() {
		var group = new StateGroup {
			GroupId = "GuardAI",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new StateDefinition {
					Transitions = [
					new() {
						TargetState = "Patrol",
						Conditions = new ConditionGroupDef {
							All = [
								new() { Signal = "has_target", Op = "==", Value = 0f }
							],
							Any = [
								new() { Signal = "time", Op = ">", Value = 10f },
								new() { Signal = "boredom", Op = ">", Value = 50f }
							],
							None = [
								new() { Signal = "alert", Op = "==", Value = 1f }
							]
						}
					}
				]
				},
				["Patrol"] = new StateDefinition()
			}
		};

		// Helper to invoke evaluate
		StateMachineEvaluator.Result Run(float hasTarget, float time, float boredom, float alert) => StateMachineEvaluator.Evaluate("Idle", group, ["GuardAI.Patrol"], s => s switch {
			"has_target" => hasTarget,
			"time" => time,
			"boredom" => boredom,
			"alert" => alert,
			_ => 0f
		});

		// Conditions should pass: no target (0), boredom > 50 (60), alert is false (0)
		Run(0f, 2f, 60f, 0f).HasValue.Should().BeTrue();

		// Fails ALL: has target (1)
		Run(1f, 15f, 60f, 0f).HasValue.Should().BeFalse();

		// Fails ANY: time < 10 (2) and boredom < 50 (10)
		Run(0f, 2f, 10f, 0f).HasValue.Should().BeFalse();

		// Fails NONE: alert is true (1)
		Run(0f, 15f, 60f, 1f).HasValue.Should().BeFalse();
	}

	[Fact]
	public void StateMachineEvaluator_HierarchicalTransitions_AppliesDepthPenalty() {
		// Arrange
		var group = new StateGroup {
			GroupId = "RobotAI",
			PriorityTier = 1,
			TierScale = 10000,
			DepthPenalty = 1000,
			States = new Dictionary<string, StateDefinition> {
				["Patrol"] = new StateDefinition {
					Transitions = [
					new() {
						TargetState = "Refuel",
						Priority = 2000, // Parent transition priority
						Conditions = new ConditionGroupDef { All = [new() { Signal = "battery", Op = "<", Value = 20f }] }
					}
				]
				},
				["AggressivePatrol"] = new StateDefinition {
					Parent = "Patrol",
					Transitions = [
					new() {
						TargetState = "Attack",
						Priority = 1500, // Child transition priority
						Conditions = new ConditionGroupDef { All = [new() { Signal = "see_enemy", Op = "==", Value = 1f }] }
					}
				]
				},
				["Refuel"] = new StateDefinition(),
				["Attack"] = new StateDefinition()
			}
		};

		// 1. In AggressivePatrol, battery is low (10) AND enemy is seen (1).
		// Parent transition priority: (1 * 10000) + 2000 - (1 * 1000) = 11000
		// Child transition priority:  (1 * 10000) + 1500 - (0 * 1000) = 11500
		// Child state transition (Attack) should win because of depth penalty on parent transition!
		var result = StateMachineEvaluator.Evaluate("AggressivePatrol", group, ["RobotAI.Refuel", "RobotAI.Attack"], s => s switch {
			"battery" => 10f,
			"see_enemy" => 1f,
			_ => 0f
		});

		result.HasValue.Should().BeTrue();
		result.TargetStateId.Should().Be("RobotAI.Attack");
	}

	[Fact]
	public void StateMachineEvaluator_RespectsSensorInfluences() {
		// Arrange
		var group = new StateGroup {
			GroupId = "CombatAI",
			PriorityTier = 1,
			TierScale = 1000,
			States = new Dictionary<string, StateDefinition> {
				["Search"] = new StateDefinition {
					Transitions = [
					new() {
						TargetState = "Attack",
						Priority = 500,
						Conditions = new ConditionGroupDef { All = [new() { Signal = "in_range", Op = "==", Value = 1f }] },
						Influences = [
							new() { Signal = "anger", Weight = 100f }
						]
					},
					new() {
						TargetState = "Flee",
						Priority = 600, // Flee has higher base priority (600 vs 500)
						Conditions = new ConditionGroupDef { All = [new() { Signal = "in_range", Op = "==", Value = 1f }] },
						Influences = [
							new() { Signal = "fear", Weight = 50f }
						]
					}
				]
				},
				["Attack"] = new StateDefinition(),
				["Flee"] = new StateDefinition()
			}
		};

		// Scenario 1: Anger is low (1.0), Fear is high (5.0).
		// Attack priority = 1000 + 500 + (1 * 100) = 1600
		// Flee priority   = 1000 + 600 + (5 * 50)  = 1850 -> Should Flee
		var result1 = StateMachineEvaluator.Evaluate("Search", group, ["CombatAI.Attack", "CombatAI.Flee"], s => s switch {
			"in_range" => 1f,
			"anger" => 1f,
			"fear" => 5f,
			_ => 0f
		});
		result1.TargetStateId.Should().Be("CombatAI.Flee");

		// Scenario 2: Anger is high (6.0), Fear is low (1.0).
		// Attack priority = 1000 + 500 + (6 * 100) = 2100
		// Flee priority   = 1000 + 600 + (1 * 50)  = 1650 -> Should Attack
		var result2 = StateMachineEvaluator.Evaluate("Search", group, ["CombatAI.Attack", "CombatAI.Flee"], s => s switch {
			"in_range" => 1f,
			"anger" => 6f,
			"fear" => 1f,
			_ => 0f
		});
		result2.TargetStateId.Should().Be("CombatAI.Attack");
	}

	[Fact]
	public void StateMachineEvaluator_FullyQualifiedCurrentState_Succeeds() {
		var group = new StateGroup {
			GroupId = "GuardAI",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new StateDefinition {
					Transitions = [
					new() {
						TargetState = "Patrol",
						Conditions = new ConditionGroupDef {
							All = [
								new() { Signal = "time", Op = ">", Value = 5f }
							]
						}
					}
				]
				},
				["Patrol"] = new StateDefinition()
			}
		};

		// Current state passed as fully-qualified "GuardAI.Idle"
		var result = StateMachineEvaluator.Evaluate("GuardAI.Idle", group, ["GuardAI.Patrol"], s => s == "time" ? 6f : 0f);
		result.HasValue.Should().BeTrue();
		result.TargetStateId.Should().Be("GuardAI.Patrol");
	}

	[Fact]
	public void StateMachineEvaluator_ExitValue_AppliesHysteresis() {
		var group = new StateGroup {
			GroupId = "TempAI",
			States = new Dictionary<string, StateDefinition> {
				["Idle"] = new StateDefinition {
					Transitions = [
					new() {
						TargetState = "Active",
						Conditions = new ConditionGroupDef {
							All = [
								new() { Signal = "temp", Op = ">", Value = 50f, ExitValue = 40f }
							]
						}
					}
				]
				},
				["Active"] = new StateDefinition {
					Transitions = [
					new() {
						TargetState = "Active",
						Conditions = new ConditionGroupDef {
							All = [
								new() { Signal = "temp", Op = ">", Value = 50f, ExitValue = 40f }
							]
						}
					}
				]
				}
			}
		};

		// 1. Current state is Idle (not at target Active). We must use Value (50f).
		// Temp is 45f -> should not transition (45 > 50 is false)
		var resultIdle = StateMachineEvaluator.Evaluate("Idle", group, ["TempAI.Active"], s => s == "temp" ? 45f : 0f);
		resultIdle.HasValue.Should().BeFalse();

		// 2. Current state is Active (at target Active). We should use ExitValue (40f).
		// Temp is 45f -> should stay Active (45 > 40 is true)
		var resultActive1 = StateMachineEvaluator.Evaluate("Active", group, ["TempAI.Active"], s => s == "temp" ? 45f : 0f);
		resultActive1.HasValue.Should().BeTrue();
		resultActive1.TargetStateId.Should().Be("TempAI.Active");

		// 3. Current state is Active. Temp is 35f -> should exit Active (35 > 40 is false)
		var resultActive2 = StateMachineEvaluator.Evaluate("Active", group, ["TempAI.Active"], s => s == "temp" ? 35f : 0f);
		resultActive2.HasValue.Should().BeFalse();
	}

	[Fact]
	public void StateMachineEvaluator_HierarchicalTransitions_FallsBackToParent() {
		// Arrange
		var group = new StateGroup {
			GroupId = "RobotAI",
			PriorityTier = 1,
			TierScale = 10000,
			DepthPenalty = 1000,
			States = new Dictionary<string, StateDefinition> {
				["Patrol"] = new StateDefinition {
					Transitions = [
					new() {
						TargetState = "Refuel",
						Priority = 2000,
						Conditions = new ConditionGroupDef { All = [new() { Signal = "battery", Op = "<", Value = 20f }] }
					}
				]
				},
				["AggressivePatrol"] = new StateDefinition {
					Parent = "Patrol",
					Transitions = [
					new() {
						TargetState = "Attack",
						Priority = 1500,
						Conditions = new ConditionGroupDef { All = [new() { Signal = "see_enemy", Op = "==", Value = 1f }] }
					}
				]
				},
				["Refuel"] = new StateDefinition(),
				["Attack"] = new StateDefinition()
			}
		};

		// In AggressivePatrol, battery is low (10) but NO enemy is seen (0).
		// Child transition (Attack) condition fails.
		// Parent transition (Refuel) condition succeeds.
		// It should fall back to Refuel.
		var result = StateMachineEvaluator.Evaluate("AggressivePatrol", group, ["RobotAI.Refuel", "RobotAI.Attack"], s => s switch {
			"battery" => 10f,
			"see_enemy" => 0f,
			_ => 0f
		});

		result.HasValue.Should().BeTrue();
		result.TargetStateId.Should().Be("RobotAI.Refuel");
	}
}

 // namespace DataCatalyst.Tests
