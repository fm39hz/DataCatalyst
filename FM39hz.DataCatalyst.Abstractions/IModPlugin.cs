namespace FM39hz.DataCatalyst.Abstractions;

public interface IModPlugin {
	string Name { get; }
	string[] Dependencies { get; }
	void OnLoad(IModGameContext context);
}
