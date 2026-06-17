namespace DataCatalyst.Abstractions;

public interface IFormatReader<TValue> {
	string FileExtension { get; }
	bool TryRead(string text, out TValue value);
}
