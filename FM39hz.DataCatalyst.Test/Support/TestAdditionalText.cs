namespace FM39hz.DataCatalyst.Test.Support;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

public sealed class TestAdditionalText(string path, string content) : AdditionalText {
	private readonly SourceText _text = SourceText.From(content);

	public override string Path { get; } = path;

	public override SourceText? GetText(CancellationToken cancellationToken = default) => _text;
}
