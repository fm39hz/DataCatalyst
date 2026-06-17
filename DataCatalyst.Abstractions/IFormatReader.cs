namespace DataCatalyst.Abstractions;

/// <summary>Parses a text format into a typed value.</summary>
public interface IFormatReader<TValue> {
	/// <summary>File extension this reader handles.</summary>
	public string FileExtension { get; }
	/// <summary>Attempts to parse the input text into a value.</summary>
	public bool TryRead(string text, out TValue value);
}
