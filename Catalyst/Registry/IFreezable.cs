namespace Catalyst.Registry;

public interface IFreezable {
    bool Frozen { get; }
    void Freeze();
}
