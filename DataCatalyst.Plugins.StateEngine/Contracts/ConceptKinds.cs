namespace DataCatalyst.Plugins.StateEngine;

/// <summary>Well-known concept kinds used by StateEngine SourceGen.</summary>
public static class ConceptKinds {
	/// <summary>Marks an enum as a state machine state type. SourceGen generates IStateMapper</summary>
	public const string State = "state";

	/// <summary>Marks an enum as a sensor type. SourceGen generates ISensorMapper</summary>
	public const string Sensor = "sensor";
}
