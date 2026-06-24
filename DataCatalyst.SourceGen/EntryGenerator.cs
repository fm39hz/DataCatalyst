using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace DataCatalyst.V2;

[Generator]
public sealed class EntryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Read JSON files from AdditionalFiles
        var jsonFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(static (text, ct) =>
            {
                try
                {
                    var content = text.GetText(ct)?.ToString();
                    if (content == null) return ImmutableArray<RawEntry>.Empty;
                    return ParseEntries(content, Path.GetFileNameWithoutExtension(text.Path));
                }
                catch
                {
                    return ImmutableArray<RawEntry>.Empty;
                }
            })
            .Collect()
            .Select(static (entries, _) =>
            {
                var all = new List<EntryData>();
                var seen = new HashSet<string>();

                foreach (var batch in entries)
                {
                    foreach (var e in batch)
                    {
                        if (seen.Add(e.Key))
                        {
                            all.Add(new EntryData(e.Key, e.Concepts.ToImmutableArray()));
                        }
                    }
                }
                return all.ToImmutableArray();
            });

        context.RegisterSourceOutput(jsonFiles, static (spc, entries) =>
        {
            if (entries.Length == 0) return;

            // Group by concept count to find multi-concept entries
            var entryTypes = new List<MemberDeclarationSyntax>();
            var initStatements = new List<StatementSyntax>();

            foreach (var entry in entries)
            {
                var typeName = SanitizeName(entry.Key);
                var conceptTypes = entry.Concepts
                    .Select(c => ParseTypeName($"global::DataCatalyst.Generated.{SanitizeName(c)}"))
                    .ToArray();

                // Generate: public record struct Goblin : IEntry, IBelongTo<Creature>, IBelongTo<Enemy> { }
                var interfaces = new List<TypeSyntax>
                {
                    ParseTypeName("global::DataCatalyst.IEntry")
                };

                foreach (var ct in conceptTypes)
                {
                    interfaces.Add(
                        ParseTypeName($"global::DataCatalyst.IBelongTo<{ct}>"));
                }

                var entryStruct = StructDeclaration(typeName)
                    .WithModifiers(TokenList(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.RecordKeyword)))
                    .WithBaseList(BaseList(SeparatedList<BaseTypeSyntax>(
                        interfaces.Select(iface => (BaseTypeSyntax)SimpleBaseType(iface)))));

                entryTypes.Add(entryStruct);

                // Generate: EntryRegistry.Register<Goblin>(typeof(Creature), typeof(Enemy));
                var registerCall = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            ParseTypeName("global::DataCatalyst.Registry.EntryRegistry"),
                            GenericName("Register")
                                .WithTypeArgumentList(TypeArgumentList(
                                    SingletonSeparatedList<TypeSyntax>(
                                        ParseTypeName($"global::DataCatalyst.Generated.Entries.{typeName}"))))))
                    .WithArgumentList(ArgumentList(
                        SeparatedList<ArgumentSyntax>(
                            entry.Concepts.Select(c =>
                                Argument(TypeOfExpression(
                                    ParseTypeName($"global::DataCatalyst.Generated.{SanitizeName(c)}")))))));

                initStatements.Add(ExpressionStatement(registerCall));
            }

            // Generate concept marker types
            var uniqueConcepts = new HashSet<string>();
            foreach (var e in entries)
                foreach (var c in e.Concepts)
                    uniqueConcepts.Add(c);

            var conceptStructs = new List<MemberDeclarationSyntax>();
            foreach (var c in uniqueConcepts)
            {
                var cs = StructDeclaration(SanitizeName(c))
                    .WithModifiers(TokenList(
                        Token(SyntaxKind.PublicKeyword)))
                    .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                        SimpleBaseType(ParseTypeName("global::DataCatalyst.IConcept")))));
                conceptStructs.Add(cs);
            }

            var conceptsNs = NamespaceDeclaration(ParseName("DataCatalyst.Generated"))
                .AddMembers(conceptStructs.ToArray());

            // Generate entry types namespace
            var entryTypesNs = NamespaceDeclaration(ParseName("DataCatalyst.Generated.Entries"))
                .AddMembers(entryTypes.ToArray());

            // Generate ModuleInitializer
            var initClass = ClassDeclaration("EntryRegistrations")
                .WithModifiers(TokenList(
                    Token(SyntaxKind.InternalKeyword),
                    Token(SyntaxKind.StaticKeyword)))
                .AddMembers(
                    MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Init")
                        .WithAttributeLists(SingletonList(
                            AttributeList(SingletonSeparatedList(
                                Attribute(ParseName("System.Runtime.CompilerServices.ModuleInitializer"))))))
                        .WithModifiers(TokenList(
                            Token(SyntaxKind.InternalKeyword),
                            Token(SyntaxKind.StaticKeyword)))
                        .WithBody(Block(initStatements)));

            var initNs = NamespaceDeclaration(ParseName("DataCatalyst.Generated"))
                .AddMembers(initClass);

            var cu = CompilationUnit()
                .WithLeadingTrivia(Comment("// <auto-generated/>"))
                .WithMembers(List(new MemberDeclarationSyntax[]
                {
                    conceptsNs,
                    entryTypesNs,
                    initNs
                }))
                .NormalizeWhitespace();

            spc.AddSource("Entries.g.cs", SourceText.From(cu.ToFullString(), Encoding.UTF8));
        });
    }

    private static ImmutableArray<RawEntry> ParseEntries(string json, string filename)
    {
        var results = new List<RawEntry>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            WalkAndDiscover(doc.RootElement, null, filename, results);
        }
        catch { }
        return results.ToImmutableArray();
    }

    private static void WalkAndDiscover(JsonElement element, string? parentKey,
        string? filename, List<RawEntry> results)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryGetConcept(element, out var concepts) && concepts.Count > 0)
            {
                var key = ExtractKey(element, parentKey, filename);
                if (key != null)
                {
                    results.Add(new RawEntry(key, concepts));
                }

                // Continue walking for nested entries
                foreach (var prop in element.EnumerateObject())
                {
                    if (IsSkippedField(prop.Name)) continue;
                    WalkAndDiscover(prop.Value, prop.Name, filename, results);
                }
            }
            else
            {
                foreach (var prop in element.EnumerateObject())
                    WalkAndDiscover(prop.Value, prop.Name, filename, results);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                WalkAndDiscover(item, null, filename, results);
        }
    }

    private static bool TryGetConcept(JsonElement obj, out List<string> concepts)
    {
        concepts = new List<string>();
        if (obj.TryGetProperty("Concept", out var prop) ||
            obj.TryGetProperty("concept", out prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
                concepts.Add(prop.GetString()!);
            else if (prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        concepts.Add(item.GetString()!);
                }
            }
        }
        return concepts.Count > 0;
    }

    private static string? ExtractKey(JsonElement obj, string? parentKey, string? filename)
    {
        if (!string.IsNullOrEmpty(parentKey)) return parentKey;

        string[] keyFields = { "$key", "_id", "Id", "id", "Name", "name", "Key", "key" };
        foreach (var field in keyFields)
        {
            if (obj.TryGetProperty(field, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        return filename;
    }

    private static bool IsSkippedField(string name)
    {
        return name == "Concept" || name == "concept" || name == "$key" ||
               name == "Id" || name == "_id" || name == "Inherits" || name == "inherits";
    }

    private static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        // Remove invalid chars, ensure starts with letter
        var sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }
        if (sb.Length == 0 || !char.IsLetter(sb[0]))
            sb.Insert(0, '_');
        return sb.ToString();
    }

    private readonly record struct RawEntry(string Key, List<string> Concepts);
    private readonly record struct EntryData(string Key, ImmutableArray<string> Concepts);
}
