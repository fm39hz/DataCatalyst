namespace DataCatalyst.Plugins.StateEngine.Contracts;

using System;

/// <summary>
/// Marks an enum as a state type for state machine evaluation.
/// SourceGen auto-generates the IStateMapper&lt;T&gt; implementation and registers it.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class DataStateEnumAttribute : Attribute {
}

/// <summary>
/// Marks an enum as a sensor type for state machine evaluation.
/// SourceGen auto-generates the ISensorMapper&lt;T&gt; implementation and registers it.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class DataSensorEnumAttribute : Attribute {
}
