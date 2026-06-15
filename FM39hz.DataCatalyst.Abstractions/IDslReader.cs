namespace FM39hz.DataCatalyst.Abstractions;

public interface IDslReader<TValue> {
	string FileExtension { get; }
	bool TryRead(string text, out TValue value);
}
