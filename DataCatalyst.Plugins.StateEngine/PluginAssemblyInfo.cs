namespace DataCatalyst.Plugins.StateEngine;

using DataCatalyst.Abstractions;

/// <summary>Evaluates state machine transitions and resolution.</summary>
[DataPlugin(DependsOn = [typeof(NumericCompare.NumericComparePlugin), typeof(Transition.TransitionPlugin)])]
public class StateEnginePlugin : IDataPlugin { }
