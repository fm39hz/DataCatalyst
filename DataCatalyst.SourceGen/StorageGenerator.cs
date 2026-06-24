using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace DataCatalyst.V2;

using RawEntry = DataCatalyst.Storage.RawEntry;

[Generator]
public sealed class StorageGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
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
            .Collect();

        var combined = context.CompilationProvider.Combine(jsonFiles);

        context.RegisterSourceOutput(combined, static (spc, input) =>
        {
            var compilation = input.Left;
            var jsonBatches = input.Right;

            // Find all aspects in compilation and referenced assemblies
            var aspectList = new List<AspectInfo>();
            AccumulateTypes(compilation.Assembly.GlobalNamespace, aspectList);
            foreach (var reference in compilation.References)
            {
                var assemblySymbol = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                if (assemblySymbol != null)
                {
                    AccumulateTypes(assemblySymbol.GlobalNamespace, aspectList);
                }
            }

            // Map lowercase aspect short name to AspectInfo
            var aspectMap = new Dictionary<string, AspectInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in aspectList)
            {
                aspectMap[a.ShortName] = a;
            }

            // Map Concept Name -> Set of AspectInfo
            var conceptAspects = new Dictionary<string, HashSet<AspectInfo>>(StringComparer.OrdinalIgnoreCase);

            // First, find all unique concepts in json files (even if they have no entries with aspects)
            // so we generate an empty aspects struct and pool for them.
            foreach (var batch in jsonBatches)
            {
                foreach (var entry in batch)
                {
                    foreach (var conceptName in entry.Concepts)
                    {
                        if (!conceptAspects.TryGetValue(conceptName, out _))
                        {
                            conceptAspects[conceptName] = new HashSet<AspectInfo>();
                        }
                    }
                }
            }

            // Populate aspects used by each concept
            foreach (var batch in jsonBatches)
            {
                foreach (var entry in batch)
                {
                    foreach (var conceptName in entry.Concepts)
                    {
                        var set = conceptAspects[conceptName];
                        foreach (var compName in entry._fieldNames)
                        {
                            if (aspectMap.TryGetValue(compName, out var a))
                            {
                                set.Add(a);
                            }
                        }
                    }
                }
            }

            var conceptMembers = new List<MemberDeclarationSyntax>();

            foreach (var kv in conceptAspects)
            {
                var conceptName = SanitizeName(kv.Key);
                var aspectsInConcept = kv.Value.OrderBy(a => a.ShortName).ToList();

                var structName = $"{conceptName}Aspects";
                var poolName = $"{conceptName}Pool";

                // Generate fields
                var fields = new List<MemberDeclarationSyntax>();
                foreach (var a in aspectsInConcept)
                {
                    var f = ParseMemberDeclaration($"public global::{a.FullType} {a.ShortName};");
                    if (f != null) fields.Add(f);
                }

                // Generate Take<T>
                var takeStatements = aspectsInConcept.Select(a =>
                    $"        if (typeof(T) == typeof(global::{a.FullType})) return ref global::System.Runtime.CompilerServices.Unsafe.As<global::{a.FullType}, T>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in {a.ShortName}));");
                var takeBody = string.Join("\n", takeStatements) + $"\n        throw new global::System.ArgumentException($\"Aspect '{{typeof(T).Name}}' not found in {structName}\");";

                var takeMethod = ParseMemberDeclaration($@"
    public ref readonly T Take<T>() where T : struct
    {{
{takeBody}
    }}");

                if (takeMethod != null) fields.Add(takeMethod);

                var aspectsStruct = StructDeclaration(structName)
                    .WithModifiers(TokenList(
                        Token(SyntaxKind.PublicKeyword)))
                    .WithMembers(List(fields));

                conceptMembers.Add(aspectsStruct);

                // Generate Pool
                var poolMembers = new List<MemberDeclarationSyntax>();
                
                poolMembers.Add(ParseMemberDeclaration(
                    $"private {structName}[] _data = global::System.Array.Empty<{structName}>();"
                )!);

                poolMembers.Add(ParseMemberDeclaration(
                    "public int Count => _data.Length;"
                )!);

                var resizeMethod = ParseMemberDeclaration($@"
    public void Resize(int size)
    {{
        global::System.Array.Resize(ref _data, size);
    }}");
                if (resizeMethod != null) poolMembers.Add(resizeMethod);

                var getMethod = ParseMemberDeclaration($@"
    public T Get<T>(int index) where T : struct
    {{
        if (index < 0 || index >= _data.Length) throw new global::System.IndexOutOfRangeException();
        return (T)(object)_data[index].Take<T>();
    }}");
                if (getMethod != null) poolMembers.Add(getMethod);

                // Set<T>
                var setStatements = aspectsInConcept.Select((a, i) =>
                {
                    var prefix = i > 0 ? "else " : "";
                    return $"        {prefix}if (typeof(T) == typeof(global::{a.FullType})) _data[index].{a.ShortName} = (global::{a.FullType})(object)value;";
                });
                var setBody = "        if (index < 0 || index >= _data.Length) throw new global::System.IndexOutOfRangeException();\n" + string.Join("\n", setStatements);
                
                var setMethod = ParseMemberDeclaration($@"
    public void Set<T>(int index, T value) where T : struct
    {{
{setBody}
    }}");
                if (setMethod != null) poolMembers.Add(setMethod);

                // SetRaw
                var setRawStatements = aspectsInConcept.Select((a, i) =>
                {
                    var prefix = i > 0 ? "else " : "";
                    return $"        {prefix}if (type == typeof(global::{a.FullType})) _data[index].{a.ShortName} = (global::{a.FullType})value;";
                });
                var setRawBody = "        if (index < 0 || index >= _data.Length) return;\n" + string.Join("\n", setRawStatements);

                var setRawMethod = ParseMemberDeclaration($@"
    public void SetRaw(int index, global::System.Type type, object value)
    {{
{setRawBody}
    }}");
                if (setRawMethod != null) poolMembers.Add(setRawMethod);

                var poolClass = ClassDeclaration(poolName)
                    .WithModifiers(TokenList(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.SealedKeyword)))
                    .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                        SimpleBaseType(ParseTypeName("global::DataCatalyst.Storage.IStoragePool")))))
                    .WithMembers(List(poolMembers));

                conceptMembers.Add(poolClass);
            }

            // Generate AspectDeserializerRegistrations to register reflection-free and format-agnostic deserializers
            var registerStatements = new List<StatementSyntax>();
            var seenAspects = new HashSet<string>();
            var typeToHelperName = new Dictionary<string, string>();
            var helperMethods = new List<MethodDeclarationSyntax>();

            foreach (var a in aspectList)
            {
                if (seenAspects.Add(a.FullType))
                {
                    var helperName = GetOrCreateHelperForType(a.Symbol, typeToHelperName, helperMethods);
                    var registerCall = $@"global::DataCatalyst.Storage.AspectTypeRegistry.RegisterDeserializer(
    typeof(global::{a.FullType}),
    (object __n) => {helperName}(__n)
);";
                    var stmt = ParseStatement(registerCall);
                    if (stmt != null) registerStatements.Add(stmt);
                }
            }

            var regMethod = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Init")
                .WithModifiers(TokenList(
                    Token(SyntaxKind.InternalKeyword),
                    Token(SyntaxKind.StaticKeyword)))
                .WithAttributeLists(SingletonList(
                    AttributeList(SingletonSeparatedList(
                        Attribute(ParseName("global::System.Runtime.CompilerServices.ModuleInitializer"))))))
                .WithBody(Block(registerStatements));

            var regClassMembers = new List<MemberDeclarationSyntax> { regMethod };
            regClassMembers.AddRange(helperMethods);

            var regClass = ClassDeclaration("AspectDeserializerRegistrations")
                .WithModifiers(TokenList(
                    Token(SyntaxKind.InternalKeyword),
                    Token(SyntaxKind.StaticKeyword)))
                .WithMembers(List(regClassMembers));

            conceptMembers.Add(regClass);

            var nsDecl = NamespaceDeclaration(ParseName("DataCatalyst.Generated"))
                .AddMembers(conceptMembers.ToArray());

            var cu = CompilationUnit()
                .WithMembers(SingletonList<MemberDeclarationSyntax>(nsDecl))
                .NormalizeWhitespace()
                .WithLeadingTrivia(TriviaList(Comment("// <auto-generated/>")).AddRange(ParseLeadingTrivia("\n#nullable enable\n")));

            spc.AddSource("Storage.g.cs", SourceText.From(cu.ToFullString(), global::System.Text.Encoding.UTF8));
        });
    }

    private static void AccumulateTypes(INamespaceSymbol ns, List<AspectInfo> list)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol subNs)
            {
                AccumulateTypes(subNs, list);
            }
            else if (member is INamedTypeSymbol type)
            {
                if (HasGameAspectAttribute(type))
                {
                    var fullType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (fullType.StartsWith("global::"))
                        fullType = fullType.Substring(8);
                    list.Add(new AspectInfo(fullType, type.Name, type));
                }
            }
        }
    }

    private static bool HasGameAspectAttribute(INamedTypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == "DataCatalyst.Attributes.GameAspectAttribute")
                return true;
        }
        return false;
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
        var chars = name.Select(c => (char.IsLetterOrDigit(c) || c == '_') ? c : '_').ToArray();
        var result = new string(chars);
        if (result.Length == 0 || !char.IsLetter(result[0]))
            result = "_" + result;
        return result;
    }

    private static string GetOrCreateHelperForType(
        ITypeSymbol type,
        Dictionary<string, string> typeToHelperName,
        List<MethodDeclarationSyntax> helperMethods)
    {
        var typeStr = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (typeToHelperName.TryGetValue(typeStr, out var existingName))
        {
            return existingName;
        }

        var helperName = $"__Deser_Helper_{typeToHelperName.Count}";
        typeToHelperName[typeStr] = helperName;

        var method = BuildHelperMethod(helperName, type, typeToHelperName, helperMethods);
        helperMethods.Add(method);

        return helperName;
    }

    private static MethodDeclarationSyntax BuildHelperMethod(
        string helperName,
        ITypeSymbol type,
        Dictionary<string, string> typeToHelperName,
        List<MethodDeclarationSyntax> helperMethods)
    {
        var typeSyntax = ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        if (type is IArrayTypeSymbol arrayType)
        {
            return BuildArrayHelper(helperName, typeSyntax, arrayType, typeToHelperName, helperMethods);
        }

        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.IsGenericType && namedType.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.List<T>")
            {
                return BuildListHelper(helperName, typeSyntax, namedType, typeToHelperName, helperMethods);
            }

            if (namedType.IsGenericType && namedType.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>")
            {
                return BuildDictHelper(helperName, typeSyntax, namedType, typeToHelperName, helperMethods);
            }

            return BuildCustomTypeHelper(helperName, typeSyntax, namedType, typeToHelperName, helperMethods);
        }

        return MethodDeclaration(typeSyntax, helperName)
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(
                Parameter(Identifier("__n")).WithType(ParseTypeName("object?")))))
            .WithBody(Block(ReturnStatement(DefaultExpression(typeSyntax))));
    }

    private static MethodDeclarationSyntax BuildArrayHelper(
        string helperName,
        TypeSyntax typeSyntax,
        IArrayTypeSymbol arrayType,
        Dictionary<string, string> typeToHelperName,
        List<MethodDeclarationSyntax> helperMethods)
    {
        var elemType = arrayType.ElementType;
        var elemTypeStr = elemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var convertExpr = BuildConvertExpr(elemType, ParseExpression("__list[__i]"), typeToHelperName, helperMethods, 0);

        var countAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName("__list"),
            IdentifierName("Count"));

        var rankSpecifier = ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(countAccess));
        var arrayTypeSyntax = ArrayType(ParseTypeName(elemTypeStr)).WithRankSpecifiers(SingletonList(rankSpecifier));
        var arrayCreation = ArrayCreationExpression(arrayTypeSyntax);
        
        var declarator = VariableDeclarator(Identifier("__arr")).WithInitializer(EqualsValueClause(arrayCreation));
        var declaration = VariableDeclaration(IdentifierName("var")).WithVariables(SingletonSeparatedList(declarator));
        var localDecl = LocalDeclarationStatement(declaration);

        var forInit = VariableDeclaration(PredefinedType(Token(SyntaxKind.IntKeyword)))
            .WithVariables(SingletonSeparatedList(
                VariableDeclarator(Identifier("__i"))
                .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))))));

        var forCondition = BinaryExpression(
            SyntaxKind.LessThanExpression,
            IdentifierName("__i"),
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("__list"), IdentifierName("Count")));

        var forIncrement = PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName("__i"));

        var forAssign = ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                ElementAccessExpression(
                    IdentifierName("__arr"),
                    BracketedArgumentList(SingletonSeparatedList(Argument(IdentifierName("__i"))))),
                convertExpr));

        var forStmt = ForStatement(Block(forAssign))
            .WithDeclaration(forInit)
            .WithCondition(forCondition)
            .WithIncrementors(SingletonSeparatedList<ExpressionSyntax>(forIncrement));

        var ifBody = Block(
            localDecl,
            forStmt,
            ReturnStatement(IdentifierName("__arr"))
        );

        var listType = ParseTypeName("global::System.Collections.Generic.List<object?>");
        var listPattern = DeclarationPattern(listType, SingleVariableDesignation(Identifier("__list")));
        var ifCondition = IsPatternExpression(IdentifierName("__n"), listPattern);
        
        var ifStmt = IfStatement(ifCondition, ifBody);

        var emptyArrayExpr = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ParseName("global::System.Array"),
                GenericName("Empty").WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(ParseTypeName(elemTypeStr))))));

        var body = Block(
            ifStmt,
            ReturnStatement(emptyArrayExpr)
        );

        return MethodDeclaration(typeSyntax, helperName)
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(
                Parameter(Identifier("__n")).WithType(ParseTypeName("object?")))))
            .WithBody(body);
    }

    private static MethodDeclarationSyntax BuildListHelper(
        string helperName,
        TypeSyntax typeSyntax,
        INamedTypeSymbol namedType,
        Dictionary<string, string> typeToHelperName,
        List<MethodDeclarationSyntax> helperMethods)
    {
        var elemType = namedType.TypeArguments[0];
        var elemTypeStr = elemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var convertExpr = BuildConvertExpr(elemType, ParseExpression("__item"), typeToHelperName, helperMethods, 0);

        var genericListType = GenericName(Identifier("global::System.Collections.Generic.List"))
            .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(ParseTypeName(elemTypeStr))));
        var listCreation = ObjectCreationExpression(genericListType).WithArgumentList(ArgumentList());
        
        var declarator = VariableDeclarator(Identifier("__res")).WithInitializer(EqualsValueClause(listCreation));
        var declaration = VariableDeclaration(IdentifierName("var")).WithVariables(SingletonSeparatedList(declarator));
        var localDecl = LocalDeclarationStatement(declaration);

        var forEachBody = Block(
            ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("__res"), IdentifierName("Add")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(convertExpr)))))
        );

        var forEachStmt = ForEachStatement(
            IdentifierName("var"),
            Identifier("__item"),
            IdentifierName("__list"),
            forEachBody);

        var listType = ParseTypeName("global::System.Collections.Generic.List<object?>");
        var listPattern = DeclarationPattern(listType, SingleVariableDesignation(Identifier("__list")));
        var ifCondition = IsPatternExpression(IdentifierName("__n"), listPattern);
        
        var ifStmt = IfStatement(ifCondition, Block(forEachStmt));

        var body = Block(
            localDecl,
            ifStmt,
            ReturnStatement(IdentifierName("__res"))
        );

        return MethodDeclaration(typeSyntax, helperName)
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(
                Parameter(Identifier("__n")).WithType(ParseTypeName("object?")))))
            .WithBody(body);
    }

    private static MethodDeclarationSyntax BuildDictHelper(
        string helperName,
        TypeSyntax typeSyntax,
        INamedTypeSymbol namedType,
        Dictionary<string, string> typeToHelperName,
        List<MethodDeclarationSyntax> helperMethods)
    {
        var keyType = namedType.TypeArguments[0];
        var valType = namedType.TypeArguments[1];
        var valTypeStr = valType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (keyType.SpecialType != SpecialType.System_String)
        {
            return MethodDeclaration(typeSyntax, helperName)
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
                .WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("__n")).WithType(ParseTypeName("object?")))))
                .WithBody(Block(ReturnStatement(DefaultExpression(typeSyntax))));
        }

        var convertExpr = BuildConvertExpr(valType, ParseExpression("__kvp.Value"), typeToHelperName, helperMethods, 0);

        var genericDictType = GenericName(Identifier("global::System.Collections.Generic.Dictionary"))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(new TypeSyntax[] {
                PredefinedType(Token(SyntaxKind.StringKeyword)),
                ParseTypeName(valTypeStr)
            })));

        var ordinalIgnoreCase = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ParseName("global::System.StringComparer"),
            IdentifierName("OrdinalIgnoreCase"));

        var dictCreation = ObjectCreationExpression(genericDictType)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(ordinalIgnoreCase))));

        var declarator = VariableDeclarator(Identifier("__res")).WithInitializer(EqualsValueClause(dictCreation));
        var declaration = VariableDeclaration(IdentifierName("var")).WithVariables(SingletonSeparatedList(declarator));
        var localDecl = LocalDeclarationStatement(declaration);

        var kvpKey = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("__kvp"), IdentifierName("Key"));
        var addCall = InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("__res"), IdentifierName("Add")))
            .WithArgumentList(ArgumentList(SeparatedList(new ArgumentSyntax[] {
                Argument(kvpKey),
                Argument(convertExpr)
            })));

        var forEachBody = Block(ExpressionStatement(addCall));
        var forEachStmt = ForEachStatement(
            IdentifierName("var"),
            Identifier("__kvp"),
            IdentifierName("__dict"),
            forEachBody);

        var dictType = ParseTypeName("global::System.Collections.Generic.Dictionary<string, object?>");
        var dictPattern = DeclarationPattern(dictType, SingleVariableDesignation(Identifier("__dict")));
        var ifCondition = IsPatternExpression(IdentifierName("__n"), dictPattern);

        var ifStmt = IfStatement(ifCondition, Block(forEachStmt));

        var body = Block(
            localDecl,
            ifStmt,
            ReturnStatement(IdentifierName("__res"))
        );

        return MethodDeclaration(typeSyntax, helperName)
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(
                Parameter(Identifier("__n")).WithType(ParseTypeName("object?")))))
            .WithBody(body);
    }

    private static MethodDeclarationSyntax BuildCustomTypeHelper(
        string helperName,
        TypeSyntax typeSyntax,
        INamedTypeSymbol namedType,
        Dictionary<string, string> typeToHelperName,
        List<MethodDeclarationSyntax> helperMethods)
    {
        var props = namedType.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && !p.IsReadOnly)
            .ToList();
        var fields = namedType.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsReadOnly && !f.IsImplicitlyDeclared)
            .ToList();

        var initializerList = new List<ExpressionSyntax>();
        int varIndex = 1;

        foreach (var p in props)
        {
            var vName = $"__v{varIndex++}";
            var convertExpr = BuildConvertExpr(p.Type, IdentifierName(vName), typeToHelperName, helperMethods, varIndex);

            ExpressionSyntax fallbackExpr = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("__default"),
                IdentifierName(p.Name));

            if (p.Type.IsReferenceType && p.Type.NullableAnnotation != NullableAnnotation.Annotated)
            {
                fallbackExpr = BinaryExpression(
                    SyntaxKind.CoalesceExpression,
                    fallbackExpr,
                    ThrowExpression(
                        ObjectCreationExpression(ParseTypeName("global::System.InvalidOperationException"))
                        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(
                            LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"Property '{p.Name}' is required and cannot be null."))))))));
            }

            var initExpr = ConditionalExpression(
                BinaryExpression(
                    SyntaxKind.LogicalAndExpression,
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("__dict"),
                            IdentifierName("TryGetValue")))
                    .WithArgumentList(ArgumentList(SeparatedList(new ArgumentSyntax[] {
                        Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(p.Name))),
                        Argument(DeclarationExpression(
                            IdentifierName("var"),
                            SingleVariableDesignation(Identifier(vName))))
                        .WithRefOrOutKeyword(Token(SyntaxKind.OutKeyword))
                    }))),
                    BinaryExpression(
                        SyntaxKind.NotEqualsExpression,
                        IdentifierName(vName),
                        LiteralExpression(SyntaxKind.NullLiteralExpression))),
                convertExpr,
                fallbackExpr
            );

            initializerList.Add(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(p.Name),
                    initExpr)
            );
        }

        foreach (var f in fields)
        {
            var vName = $"__v{varIndex++}";
            var convertExpr = BuildConvertExpr(f.Type, IdentifierName(vName), typeToHelperName, helperMethods, varIndex);

            ExpressionSyntax fallbackExpr = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("__default"),
                IdentifierName(f.Name));

            if (f.Type.IsReferenceType && f.Type.NullableAnnotation != NullableAnnotation.Annotated)
            {
                fallbackExpr = BinaryExpression(
                    SyntaxKind.CoalesceExpression,
                    fallbackExpr,
                    ThrowExpression(
                        ObjectCreationExpression(ParseTypeName("global::System.InvalidOperationException"))
                        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(
                            LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"Field '{f.Name}' is required and cannot be null."))))))));
            }

            var initExpr = ConditionalExpression(
                BinaryExpression(
                    SyntaxKind.LogicalAndExpression,
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("__dict"),
                            IdentifierName("TryGetValue")))
                    .WithArgumentList(ArgumentList(SeparatedList(new ArgumentSyntax[] {
                        Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(f.Name))),
                        Argument(DeclarationExpression(
                            IdentifierName("var"),
                            SingleVariableDesignation(Identifier(vName))))
                        .WithRefOrOutKeyword(Token(SyntaxKind.OutKeyword))
                    }))),
                    BinaryExpression(
                        SyntaxKind.NotEqualsExpression,
                        IdentifierName(vName),
                        LiteralExpression(SyntaxKind.NullLiteralExpression))),
                convertExpr,
                fallbackExpr
            );

            initializerList.Add(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(f.Name),
                    initExpr)
            );
        }

        var body = Block(
            IfStatement(
                PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    ParenthesizedExpression(
                        IsPatternExpression(
                            IdentifierName("__n"),
                            DeclarationPattern(
                                ParseTypeName("global::System.Collections.Generic.Dictionary<string, object?>"),
                                SingleVariableDesignation(Identifier("__dict")))))),
                ReturnStatement(
                    ObjectCreationExpression(typeSyntax).WithArgumentList(ArgumentList()))),
            LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier("__default"))
                    .WithInitializer(EqualsValueClause(
                        ObjectCreationExpression(typeSyntax).WithArgumentList(ArgumentList())))))),
            ReturnStatement(
                ObjectCreationExpression(typeSyntax)
                .WithInitializer(
                    InitializerExpression(
                        SyntaxKind.ObjectInitializerExpression,
                        SeparatedList(initializerList))))
        );

        return MethodDeclaration(typeSyntax, helperName)
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(
                Parameter(Identifier("__n")).WithType(ParseTypeName("object?")))))
            .WithBody(body);
    }

    private static ExpressionSyntax BuildConvertExpr(
        ITypeSymbol type,
        ExpressionSyntax inputExpr,
        Dictionary<string, string> typeToHelperName,
        List<MethodDeclarationSyntax> helperMethods,
        int varIndex)
    {
        var underlyingType = type;
        bool isNullable = false;

        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            underlyingType = named.TypeArguments[0];
            isNullable = true;
        }

        ExpressionSyntax coreExpr;

        if (underlyingType.SpecialType == SpecialType.System_String)
        {
            coreExpr = ParseExpression($"({inputExpr} is string __ts_{varIndex} ? __ts_{varIndex} : ({inputExpr} != null ? {inputExpr}.ToString()! : null!))");
        }
        else if (underlyingType.SpecialType == SpecialType.System_Int32)
        {
            coreExpr = ParseExpression($"({inputExpr} is int __ti_{varIndex} ? __ti_{varIndex} : ({inputExpr} is double __tdi_{varIndex} ? (int)__tdi_{varIndex} : ({inputExpr} != null ? global::System.Convert.ToInt32({inputExpr}) : 0)))");
        }
        else if (underlyingType.SpecialType == SpecialType.System_Single)
        {
            coreExpr = ParseExpression($"({inputExpr} is float __tf_{varIndex} ? __tf_{varIndex} : ({inputExpr} is double __tdf_{varIndex} ? (float)__tdf_{varIndex} : ({inputExpr} != null ? global::System.Convert.ToSingle({inputExpr}) : 0.0f)))");
        }
        else if (underlyingType.SpecialType == SpecialType.System_Double)
        {
            coreExpr = ParseExpression($"({inputExpr} is double __td_{varIndex} ? __td_{varIndex} : ({inputExpr} != null ? global::System.Convert.ToDouble({inputExpr}) : 0.0))");
        }
        else if (underlyingType.SpecialType == SpecialType.System_Boolean)
        {
            coreExpr = ParseExpression($"({inputExpr} is bool __tb_{varIndex} ? __tb_{varIndex} : ({inputExpr} != null ? global::System.Convert.ToBoolean({inputExpr}) : false))");
        }
        else if (underlyingType.TypeKind == TypeKind.Enum)
        {
            var enumTypeStr = underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            coreExpr = ParseExpression($"({inputExpr} is string __tse_{varIndex} ? global::System.Enum.Parse<{enumTypeStr}>(__tse_{varIndex}, true) : ({inputExpr} != null ? ({enumTypeStr})global::System.Convert.ToInt32({inputExpr}) : default({enumTypeStr})))");
        }
        else
        {
            var helperName = GetOrCreateHelperForType(underlyingType, typeToHelperName, helperMethods);
            coreExpr = InvocationExpression(
                IdentifierName(helperName),
                ArgumentList(SingletonSeparatedList(Argument(inputExpr))));
        }

        if (isNullable)
        {
            var nullableTypeStr = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return ParenthesizedExpression(
                ConditionalExpression(
                    BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        inputExpr,
                        LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    CastExpression(ParseTypeName(nullableTypeStr), LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    CastExpression(ParseTypeName(nullableTypeStr), coreExpr)));
        }

        return coreExpr;
    }

    private readonly record struct AspectInfo(string FullType, string ShortName, INamedTypeSymbol Symbol);
}
