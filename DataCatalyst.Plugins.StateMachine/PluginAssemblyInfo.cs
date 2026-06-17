namespace DataCatalyst.Plugins.StateMachine;

using DataCatalyst.Abstractions;
using DataCatalyst.Plugins.NumericCompare;
using DataCatalyst.Plugins.Transition;

/// <summary>Evaluates state machine transitions and resolution.</summary>
[DataPlugin(DependsOn = [typeof(NumericCompare.NumericComparePlugin), typeof(Transition.TransitionPlugin)])]
public class StateMachinePlugin : IDataPlugin { }
