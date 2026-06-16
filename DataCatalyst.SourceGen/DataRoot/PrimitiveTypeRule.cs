namespace DataCatalyst.DataRoot;

using System.Collections.Generic;

public interface IPrimitiveTypeRule {
    string Name { get; }
    int Rank { get; }
    bool TryInfer(string rawValue);
    string EmitLiteral(string rawValue);
    string EmitDefault();
}

public static class PrimitiveRegistry {
    private static readonly List<IPrimitiveTypeRule> _rules = new();

    static PrimitiveRegistry() {
        _rules.Add(new IntPrimitiveRule());
        _rules.Add(new LongPrimitiveRule());
        _rules.Add(new FloatPrimitiveRule());
        _rules.Add(new BoolPrimitiveRule());
        _rules.Add(new StringPrimitiveRule());
    }

    public static void Register(IPrimitiveTypeRule rule) => _rules.Add(rule);

    public static IReadOnlyList<IPrimitiveTypeRule> Rules => _rules;

    public static int WideningRank(string type) {
        foreach (var r in _rules)
            if (r.Name == type) return r.Rank;
        return -1;
    }

    public static string? Widen(string from, string to) {
        var fRank = WideningRank(from);
        var tRank = WideningRank(to);
        if (fRank < 0 || tRank < 0) return null;
        return fRank <= tRank ? to : null;
    }
}

public sealed class IntPrimitiveRule : IPrimitiveTypeRule {
    public string Name => "int";
    public int Rank => 1;
    public bool TryInfer(string raw) => int.TryParse(raw, out _);
    public string EmitLiteral(string raw) => raw;
    public string EmitDefault() => "0";
}

public sealed class LongPrimitiveRule : IPrimitiveTypeRule {
    public string Name => "long";
    public int Rank => 2;
    public bool TryInfer(string raw) => long.TryParse(raw, out _);
    public string EmitLiteral(string raw) => raw + "L";
    public string EmitDefault() => "0L";
}

public sealed class FloatPrimitiveRule : IPrimitiveTypeRule {
    public string Name => "float";
    public int Rank => 3;
    public bool TryInfer(string raw) => float.TryParse(raw, out _);
    public string EmitLiteral(string raw) => raw + "f";
    public string EmitDefault() => "0f";
}

public sealed class BoolPrimitiveRule : IPrimitiveTypeRule {
    public string Name => "bool";
    public int Rank => -1;
    public bool TryInfer(string raw) => bool.TryParse(raw, out _);
    public string EmitLiteral(string raw) => raw.ToLowerInvariant();
    public string EmitDefault() => "false";
}

public sealed class StringPrimitiveRule : IPrimitiveTypeRule {
    public string Name => "string";
    public int Rank => -1;
    public bool TryInfer(string raw) => true;
    public string EmitLiteral(string raw) => "\"" + raw.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    public string EmitDefault() => "\"\"";
}
