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

using RawEntry = DataCatalyst.Storage.RawEntry;

[Generator]
public sealed class EntryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Read JSON files from AdditionalFiles
        var jsonFiles = context.AdditionalTextsProvider
            .Where(static file => LoaderRegistry.TryGetLoader(Path.GetExtension(file.Path), out _))
            .Select(static (text, ct) =>
            {
                try
                {
                    var content = text.GetText(ct)?.ToString();
                    if (content == null) return ImmutableArray<RawEntry>.Empty;
                    return ParseEntries(content, text.Path);
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

            // Generate EntryIndexAssigner class
            var assignStatements = new List<StatementSyntax>();
            foreach (var entry in entries)
            {
                var typeName = SanitizeName(entry.Key);
                var fullEntryType = $"global::DataCatalyst.Generated.Entries.{typeName}";
                var ifStatement = ParseStatement(
                    $"if (entryType == typeof({fullEntryType})) global::DataCatalyst.Registry.EntryIndex<{fullEntryType}>.Value = index;\n"
                );
                if (ifStatement != null) assignStatements.Add(ifStatement);
            }

            var assignClass = ClassDeclaration("EntryIndexAssigner")
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.StaticKeyword)))
                .AddMembers(
                    MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Assign")
                        .WithModifiers(TokenList(
                            Token(SyntaxKind.PublicKeyword),
                            Token(SyntaxKind.StaticKeyword)))
                        .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(new[] {
                            Parameter(Identifier("entryType")).WithType(ParseTypeName("global::System.Type")),
                            Parameter(Identifier("index")).WithType(PredefinedType(Token(SyntaxKind.IntKeyword)))
                        })))
                        .WithBody(Block(assignStatements)));

            entryTypes.Add(assignClass);

            // Register index assigner
            initStatements.Insert(0, ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ParseTypeName("global::DataCatalyst.Registry.EntryRegistry"),
                        IdentifierName("RegisterIndexAssigner")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                    Argument(ParseExpression("global::DataCatalyst.Generated.Entries.EntryIndexAssigner.Assign")))))));

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

            // Register pool factories for each concept
            foreach (var c in uniqueConcepts)
            {
                var conceptName = SanitizeName(c);
                var conceptType = $"global::DataCatalyst.Generated.{conceptName}";
                var poolType = $"global::DataCatalyst.Generated.{conceptName}Pool";
                
                var registerPoolCall = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            ParseTypeName("global::DataCatalyst.Registry.EntryRegistry"),
                            IdentifierName("RegisterPool")))
                    .WithArgumentList(ArgumentList(SeparatedList(new[] {
                        Argument(TypeOfExpression(ParseTypeName(conceptType))),
                        Argument(ParenthesizedLambdaExpression()
                            .WithBody(ObjectCreationExpression(ParseTypeName(poolType))
                                .WithArgumentList(ArgumentList())))
                    }))));
                
                initStatements.Add(registerPoolCall);
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

    private static ImmutableArray<RawEntry> ParseEntries(string content, string path)
    {
        try
        {
            var ext = Path.GetExtension(path);
            if (LoaderRegistry.TryGetLoader(ext, out var loader))
            {
                var filename = Path.GetFileNameWithoutExtension(path);
                var result = loader.Load(content, filename);
                return result.Entries.Cast<RawEntry>().ToImmutableArray();
            }
            return ImmutableArray<RawEntry>.Empty;
        }
        catch
        {
            return ImmutableArray<RawEntry>.Empty;
        }
    }

    private static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        // Remove invalid chars, ensure starts with letter
        var chars = name.Select(c => (char.IsLetterOrDigit(c) || c == '_') ? c : '_').ToArray();
        var result = new string(chars);
        if (result.Length == 0 || !char.IsLetter(result[0]))
            result = "_" + result;
        return result;
    }

    private readonly record struct EntryData(string Key, ImmutableArray<string> Concepts);
}
