using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using FM39hz.DataCatalyst.DataRoot;

namespace FM39hz.DataCatalyst;

[Generator]
public sealed class DataRootGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        context.RegisterPostInitializationOutput(static ctx => {
            ctx.AddSource("DataRootAttribute.g.cs", SourceText.From("""
                using System;
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
                public sealed class DataRootAttribute : Attribute {
                    public string? Directory { get; init; }
                    public System.Type? Template { get; init; }
                }
                """, Encoding.UTF8));
        });

        var dataRoots = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => ExtractDataRoot(ctx))
            .Where(static r => r is not null)
            .Select(static (r, _) => r!.Value)
            .Collect();

        var additionalFiles = context.AdditionalTextsProvider
            .Where(static t => t.Path.EndsWith(".json"))
            .Select(static (t, _) => (Path: t.Path, Content: t.GetText()?.ToString() ?? ""))
            .Where(static t => t.Content.Length > 0)
            .Collect();

        var combined = dataRoots.Combine(additionalFiles);

        context.RegisterSourceOutput(combined, static (spc, payload) => {
            var (roots, files) = payload;
            if (roots.IsDefaultOrEmpty) return;

            foreach (var (rootPrefix, templateType, className, classNs) in roots) {
                var scanner = new DataRootScanner();

                if (templateType is not null) {
                    var tf = ImmutableArray.CreateBuilder<FieldDefinition>();
                    foreach (var member in templateType.GetMembers()) {
                        if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } p)
                            tf.Add(CreateFieldFromSymbol(p));
                        if (member is IFieldSymbol { IsStatic: false, IsConst: false, DeclaredAccessibility: Accessibility.Public } f)
                            tf.Add(CreateFieldFromSymbol(f));
                    }
                    scanner.SetTemplateFields(tf.ToImmutable());
                }

                scanner.Scan(rootPrefix, files);
                var graph = new InheritanceGraph();
                foreach (var s in scanner.Schemas) graph.AddSchema(s);
                foreach (var d in scanner.DataFiles) graph.AddNode(d);

                var ns = BuildNamespace(classNs, rootPrefix);
                var emitter = new NativePocoEmitter(graph, ns, className);
                var code = emitter.EmitAll();

                if (code.Length > 0)
                    spc.AddSource($"{className}.DataCatalyst.g.cs", SourceText.From(code, Encoding.UTF8));
            }
        });
    }

    private static FieldDefinition CreateFieldFromSymbol(ISymbol symbol) {
        var typeName = symbol switch {
            IPropertySymbol p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            IFieldSymbol f => f.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            _ => "string",
        };
        var csType = typeName switch {
            "int" => "int", "long" => "long", "float" => "float",
            "double" => "double", "bool" => "bool", "string" => "string",
            _ => "string",
        };
        return new FieldDefinition(symbol.Name, csType);
    }

    private static string BuildNamespace(string classNs, string rootPrefix) {
        return classNs.Length > 0 ? classNs : rootPrefix.TrimEnd('/').Replace('/', '.').Replace('\\', '.');
    }

    private static (string RootPrefix, INamedTypeSymbol? TemplateType, string ClassName, string ClassNs)? ExtractDataRoot(GeneratorSyntaxContext ctx) {
        if (ctx.Node is not ClassDeclarationSyntax cds) return null;
        var model = ctx.SemanticModel;
        var type = model.GetDeclaredSymbol(cds) as INamedTypeSymbol;
        if (type is null) return null;

        foreach (var attr in type.GetAttributes()) {
            if (attr.AttributeClass?.Name != "DataRootAttribute") continue;

            // Default directory = "Data/"
            var normalized = "Data/";
            foreach (var na in attr.NamedArguments) {
                if (na.Key == "Directory" && na.Value.Value is string dir) {
                    normalized = dir.Replace('\\', '/');
                    if (!normalized.EndsWith("/")) normalized += "/";
                }
            }

            INamedTypeSymbol? template = null;
            foreach (var na in attr.NamedArguments) {
                if (na.Key == "Template" && na.Value.Value is INamedTypeSymbol t)
                    template = t;
            }

            var ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
            return (normalized, template, type.Name, ns);
        }

        return null;
    }
}
