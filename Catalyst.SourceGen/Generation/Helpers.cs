namespace Catalyst.SourceGen.Generation;

using System;
using System.Collections.Generic;
using System.Linq;
using Catalyst.SourceGen.Models;
using Microsoft.CodeAnalysis;

internal static class Helpers {
    public static AspectInfo? GetAspectInfo(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken token) {
        if (context.TargetSymbol is not INamedTypeSymbol symbol) {
            return null;
        }

        var name = symbol.Name;
        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "Catalyst.Generated";

        var props = new List<string>();
        foreach (var member in symbol.GetMembers()) {
            if (member.IsStatic) {
                continue;
            }

            if (member.DeclaredAccessibility != Accessibility.Public) {
                continue;
            }

            if (member is IPropertySymbol prop) {
                if (prop.IsReadOnly) {
                    continue;
                }

                var typeStr = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                props.Add($"{prop.Name}:{typeStr}");
            }
            else if (member is IFieldSymbol field) {
                if (field.IsConst) {
                    continue;
                }

                var typeStr = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                props.Add($"{field.Name}:{typeStr}");
            }
        }
        props.Sort();
        return new AspectInfo(name, ns, string.Join(";", props));
    }

    public static ConceptInfo? GetConceptInfo(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken token) {
        if (context.TargetSymbol is not INamedTypeSymbol symbol) {
            return null;
        }

        var name = symbol.Name;
        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "Catalyst.Generated";

        return new ConceptInfo(name, ns);
    }

    public static string Sanitize(string n) {
        if (string.IsNullOrEmpty(n)) {
            return "Unknown";
        }

        var c = n.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        if (c.Length == 0) return "_";
        c[0] = char.IsLetter(c[0]) ? char.ToUpperInvariant(c[0]) : '_';
        return new string(c);
    }
}
