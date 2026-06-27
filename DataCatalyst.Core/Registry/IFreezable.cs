namespace DataCatalyst.Registry;

public interface IFreezable {
    bool Frozen { get; }
    void Freeze();
}
