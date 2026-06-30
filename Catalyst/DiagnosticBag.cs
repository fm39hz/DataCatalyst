namespace Catalyst;

using System.Collections.Generic;

public sealed class DiagnosticBag {
	private readonly List<DiagnosticItem> _items = [];

	public IReadOnlyList<DiagnosticItem> Items => _items;
	public bool HasErrors { get; private set; }

	public void Debug(string message) => _items.Add(new DiagnosticItem(message, Severity.Debug));
	public void Info(string message) => _items.Add(new DiagnosticItem(message, Severity.Info));
	public void Warn(string message) => _items.Add(new DiagnosticItem(message, Severity.Warning));
	public void Error(string message) { HasErrors = true; _items.Add(new DiagnosticItem(message, Severity.Error)); }
}

public readonly struct DiagnosticItem(string message, Severity severity) {
	public string Message { get; } = message;
	public Severity Severity { get; } = severity;
	public override string ToString() => $"[{Severity}] {Message}";
}

public enum Severity { Debug, Info, Warning, Error }
