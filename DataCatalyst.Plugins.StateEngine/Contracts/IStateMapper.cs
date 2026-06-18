namespace DataCatalyst.Plugins.StateEngine.Contracts;

/// <summary>Maps a string state key to a consumer-defined type (e.g. enum).</summary>
public interface IStateMapper<TState> {
	/// <summary>Converts a resolved state key (e.g. "GuardAI.Patrol") to a typed state identifier.</summary>
	TState MapState(string stateKey, string groupId);
}

/// <summary>Maps a string sensor signal name to a consumer-defined type (e.g. enum).</summary>
public interface ISensorMapper<TSensor> {
	/// <summary>Converts a sensor signal string to a typed sensor identifier.</summary>
	TSensor MapSensor(string signal);
}
