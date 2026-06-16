namespace DataCatalyst.Abstractions;

public interface IDslReader<TValue> {
	string FileExtension { get; }
	bool TryRead(string text, out TValue value);
}
