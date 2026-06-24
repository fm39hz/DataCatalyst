using System;
using System.Collections.Generic;

namespace DataCatalyst;

public sealed class DiagnosticBag
{
    private readonly List<string> _items = new();

    public IReadOnlyList<string> Items => _items;
    public bool HasErrors { get; private set; }

    public void Info(string message) => _items.Add($"[Info] {message}");
    public void Warn(string message) => _items.Add($"[Warn] {message}");
    public void Error(string message)
    {
        HasErrors = true;
        _items.Add($"[Error] {message}");
    }
}
