namespace DataCatalyst.Generated;

using DataCatalyst.Abstractions;

[DataComponent]
public struct Health {
	public int Current;
	public int Max;
}

[DataComponent]
public struct Damage {
	public int Min;
	public int Max;
}

[DataComponent]
public struct ExperienceReward {
	public int Value;
}

[DataComponent]
public struct Amount {
	public int Value;
}

[DataComponent]
public struct Label {
	public string Value;
}

[DataComponent]
public struct Durability {
	public int Value;
}

[DataComponent]
public struct CurrentAIState {
	public int StateId;
}
