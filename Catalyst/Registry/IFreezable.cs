namespace Catalyst.Registry;

public interface IFreezable {
	public bool Frozen { get; }
	public void Freeze();
}
