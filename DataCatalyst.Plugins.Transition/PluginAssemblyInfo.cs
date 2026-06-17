namespace DataCatalyst.Plugins.Transition;

using DataCatalyst.Abstractions;

/// <summary>Manages state transition definitions.</summary>
[DataPlugin(DependsOn = [typeof(NumericCompare.NumericComparePlugin)])]
public class TransitionPlugin : IDataPlugin { }
