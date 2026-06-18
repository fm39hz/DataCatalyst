namespace DataCatalyst.Plugins.StateEngine;

using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using DataCatalyst.Core;
using DataCatalyst.Plugins.NumericCompare.Core;
using DataCatalyst.Plugins.Transition.Models;

/// <summary>Validates state machine data at catalog resolution time.</summary>
[DataPlugin(DependsOn = [typeof(NumericCompare.NumericComparePlugin), typeof(Transition.TransitionPlugin)])]
public class StateEnginePlugin : ICatalogPlugin {
	void ICatalogPlugin.OnCatalogResolved(DataCatalog catalog, List<string> diagnostics) {
		foreach (var entry in catalog.Entries.Values) {
			if (!(entry.Components.TryGetValue(typeof(Models.StateGroup), out var raw) && raw is Models.StateGroup group)) {
				continue;
			}

			foreach (var (key, state) in group.States) {
				// Validate parent reference
				if (state.Parent != null && !group.States.ContainsKey(state.Parent)) {
					diagnostics.Add($"StateGroup '{group.GroupId}': state '{key}' references missing parent '{state.Parent}'.");
				}

				if (state.Transitions == null) continue;

				foreach (var t in state.Transitions) {
					// Validate target state
					var targetKey = t.TargetState.Contains(".") ? t.TargetState.Split('.').Last() : t.TargetState;
					if (!group.States.ContainsKey(targetKey) && targetKey != key) {
						diagnostics.Add($"StateGroup '{group.GroupId}': transition from '{key}' to '{t.TargetState}' references unknown state.");
					}

					// Validate sensor operators are parseable
					if (t.Conditions != null) {
						void CheckConds(List<SensorConditionDef>? list) {
							if (list == null) return;
							foreach (var c in list) {
								try { OperatorParser.Parse(c.Op); }
								catch (ArgumentException) {
									diagnostics.Add($"StateGroup '{group.GroupId}': invalid operator '{c.Op}' in condition of transition '{t.TargetState}'.");
								}
							}
						}
						CheckConds(t.Conditions.All);
						CheckConds(t.Conditions.Any);
						CheckConds(t.Conditions.None);
					}
				}
			}
		}
	}

}

