namespace DataCatalyst.Plugins.StateMachine;

using DataCatalyst.Abstractions;

/// <summary>Evaluates state machine transitions and resolution.</summary>
[DataPlugin(DependsOn = [typeof(NumericCompare.NumericComparePlugin), typeof(Transition.TransitionPlugin)])]
public class StateMachinePlugin : IDataPlugin { }
