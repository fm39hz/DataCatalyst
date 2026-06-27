namespace DataCatalyst.SourceGen.Generation;

using System;
using System.Collections.Generic;
using System.Linq;
using DataCatalyst.SourceGen.Models;
using Microsoft.CodeAnalysis;

internal static class SymbolScanner {
    public static void FindConceptsInNamespace(INamespaceSymbol ns, INamedTypeSymbol? conceptAttr, INamedTypeSymbol? conceptInterface, HashSet<string> conceptNames, Dictionary<string, string>? conceptNsMap = null) {
        foreach (var member in ns.GetMembers()) {
            if (member is INamespaceSymbol nestedNs) {
                FindConceptsInNamespace(nestedNs, conceptAttr, conceptInterface, conceptNames, conceptNsMap);
            }
            else if (member is INamedTypeSymbol typeSymbol) {
                if (IsConcept(typeSymbol, conceptAttr, conceptInterface)) {
                    conceptNames.Add(typeSymbol.Name);
                    if (conceptNsMap != null) {
                        var nsName = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "DataCatalyst.Generated";
                        if (!conceptNsMap.ContainsKey(typeSymbol.Name)) conceptNsMap[typeSymbol.Name] = nsName;
                    }
                }
            }
        }
    }

    public static void FindAspectsInNamespaceEx(INamespaceSymbol ns, INamedTypeSymbol? aspectAttr, HashSet<string> aspectNames, List<AspectInfo?> results) {
        foreach (var member in ns.GetMembers()) {
            if (member is INamespaceSymbol nestedNs) {
                FindAspectsInNamespaceEx(nestedNs, aspectAttr, aspectNames, results);
            }
            else if (member is INamedTypeSymbol typeSymbol && aspectAttr != null
                && typeSymbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, aspectAttr))) {
                var name = typeSymbol.Name;
                var nsName = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "DataCatalyst";
                aspectNames.Add(name);
                var props = new List<string>();
                foreach (var m in typeSymbol.GetMembers()) {
                    if (m.IsStatic || m.DeclaredAccessibility != Accessibility.Public) {
                        continue;
                    }

                    if (m is IPropertySymbol prop && !prop.IsReadOnly) {
                        props.Add($"{prop.Name}:{prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
                    }
                    else if (m is IFieldSymbol field && !field.IsConst) {
                        props.Add($"{field.Name}:{field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
                    }
                }
                props.Sort();
                results.Add(new AspectInfo(name, nsName, string.Join(";", props)));
            }
        }
    }

    public static bool IsConcept(INamedTypeSymbol typeSymbol, INamedTypeSymbol? conceptAttr, INamedTypeSymbol? conceptInterface) {
        if (conceptAttr != null && typeSymbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, conceptAttr))) {
            return true;
        }

        if (conceptInterface != null && typeSymbol.AllInterfaces.Any(it => SymbolEqualityComparer.Default.Equals(it, conceptInterface))) {
            return true;
        }

        return false;
    }
}
