namespace DataCatalyst.SourceGen.Generation;

using System.Text;
using Microsoft.CodeAnalysis.Text;

internal sealed class CodeWriter {
    private readonly StringBuilder _sb = new();
    private int _indent;

    public void Line(string text = "") {
        for (int i = 0; i < _indent; i++) _sb.Append("    ");
        _sb.AppendLine(text);
    }

    public CodeWriter Block(string header) {
        Line(header);
        Line("{");
        _indent++;
        return this;
    }

    public void EndBlock() {
        _indent--;
        Line("}");
    }

    public void Indent() => _indent++;
    public void Unindent() => _indent--;

    public bool HasContent => _sb.Length > 0;
    public override string ToString() => _sb.ToString();
    public SourceText ToSourceText() => SourceText.From(_sb.ToString(), Encoding.UTF8);
}
