using System.Collections.Immutable;
namespace DataCatalyst;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

[Generator]
public sealed class MetadataGenerator : IIncrementalGenerator {
	private static readonly DiagnosticDescriptor MissingConceptError = new(
		id: "DC100",
		title: "Entry missing Concept field",
		messageFormat: "Entry '{0}' has no 'Concept' field. Every entry must belong to at least one concept.",
		category: "DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		var jsonFiles = context.AdditionalTextsProvider
			.Where(static f => f.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
			.Select(static (f, _) => {
				var key = Path.GetFileNameWithoutExtension(f.Path);
				var text = f.GetText()?.ToString() ?? string.Empty;
				var dir = Path.GetDirectoryName(f.Path) ?? "";
				return (FileName: key, Text: text, Dir: dir);
			})
			.Where(static t => !string.IsNullOrEmpty(t.Text) && t.FileName != "concepts")
			.Collect();

		var configs = ConfigHelper.GetConfigs(context);
		var combined = jsonFiles.Combine(configs);

		context.RegisterSourceOutput(combined, static (spc, data) => {
			var (files, configs) = data;
			var components = new Dictionary<string, List<KeyValuePair<string, string>>>();
			var processed = new HashSet<string>();
			var entryConcepts = new Dictionary<string, string>();
			var allEntryKeys = new List<string>();

			foreach (var file in files) {
				if (!string.Equals(file.FileName, "datacatalyst", StringComparison.OrdinalIgnoreCase)) {
					ProcessJson(file.Text, file.FileName, components, processed, entryConcepts, allEntryKeys);
				}
			}

			if (components.Count == 0) return;

			SourceConfig matchedConfig = new("", "DataCatalyst.Generated", new List<string>());
			foreach (var file in files) {
				if (!string.Equals(file.FileName, "datacatalyst", StringComparison.OrdinalIgnoreCase)) {
					var m = MatchConfig(file.Dir, configs);
					if (m != null) { matchedConfig = m; break; }
				}
			}

			EmitComponentStructs(spc, components, matchedConfig);
			EmitRegistrations(spc, components, entryConcepts);
			EmitMerge(spc, components);
			EmitEntryConstants(spc, entryConcepts, allEntryKeys);
		});
	}

	private static void ProcessJson(string json, string defaultEntryName,
		Dictionary<string, List<KeyValuePair<string, string>>> components,
		HashSet<string> processed,
		Dictionary<string, string> entryConcepts,
		List<string> allEntryKeys) {

		try {
			using var doc = JsonDocument.Parse(json);

			if (TryGetConceptProperty(doc.RootElement, out var rootConcept)) {
				allEntryKeys.Add(defaultEntryName);
				if (!string.IsNullOrEmpty(rootConcept))
					entryConcepts[defaultEntryName] = rootConcept;

				foreach (var prop in doc.RootElement.EnumerateObject()) {
						if (IsWellKnown(prop.Name)) continue;

						if (prop.Value.ValueKind == JsonValueKind.Object) {
							if (!processed.Contains(prop.Name)) {
								var fields = new List<KeyValuePair<string, string>>();
								ExtractFields(prop.Name, prop.Value, fields, components, processed);
								if (fields.Count > 0) {
									components[prop.Name] = fields;
									processed.Add(prop.Name);
								}
							}
						}
						else {
							if (!processed.Contains(prop.Name)) {
								var inferredType = InferType(prop.Name, prop.Value, components, processed);
								components[prop.Name] = new List<KeyValuePair<string, string>> {
									new KeyValuePair<string, string>("Value", inferredType)
								};
								processed.Add(prop.Name);
							}
						}
					}
			}
			else {
				foreach (var prop in doc.RootElement.EnumerateObject()) {
					if (string.Equals(prop.Name, "inherits", StringComparison.OrdinalIgnoreCase)) continue;
					if (prop.Value.ValueKind != JsonValueKind.Object) continue;

					var entryName = prop.Name;
					allEntryKeys.Add(entryName);

					if (TryGetConceptProperty(prop.Value, out var concept))
						entryConcepts[entryName] = concept;

					foreach (var innerProp in prop.Value.EnumerateObject()) {
							if (IsWellKnown(innerProp.Name)) continue;
							if (!processed.Contains(innerProp.Name)) {
								if (innerProp.Value.ValueKind == JsonValueKind.Object) {
									var nestedFields = new List<KeyValuePair<string, string>>();
									ExtractFields(innerProp.Name, innerProp.Value, nestedFields, components, processed);
									if (nestedFields.Count > 0) {
										components[innerProp.Name] = nestedFields;
										processed.Add(innerProp.Name);
									}
								}
								else {
									var inferredType = InferType(innerProp.Name, innerProp.Value, components, processed);
									components[innerProp.Name] = new List<KeyValuePair<string, string>> {
										new KeyValuePair<string, string>("Value", inferredType)
									};
									processed.Add(innerProp.Name);
								}
							}
						}
				}
			}
		}
		catch { }
	}

	private static bool IsConceptProperty(string name) =>
		string.Equals(name, "Concept", StringComparison.Ordinal) ||
		string.Equals(name, "concept", StringComparison.Ordinal);

	private static bool IsWellKnown(string name) =>
		IsConceptProperty(name) ||
		string.Equals(name, "inherits", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(name, "layer", StringComparison.OrdinalIgnoreCase);

	private static bool TryGetConceptProperty(JsonElement element, out string? concept) {
		if (element.TryGetProperty("Concept", out var val) && val.ValueKind == JsonValueKind.String) {
			concept = val.GetString();
			return !string.IsNullOrEmpty(concept);
		}
		if (element.TryGetProperty("concept", out val) && val.ValueKind == JsonValueKind.String) {
			concept = val.GetString();
			return !string.IsNullOrEmpty(concept);
		}
		concept = null;
		return false;
	}

	private static void ExtractFields(string typeName, JsonElement obj,
		List<KeyValuePair<string, string>> fields,
		Dictionary<string, List<KeyValuePair<string, string>>> components,
		HashSet<string> processed) {

		foreach (var prop in obj.EnumerateObject()) {
			var fieldType = InferType(prop.Name, prop.Value, components, processed);
			fields.Add(new KeyValuePair<string, string>(prop.Name, fieldType));
		}

		if (fields.Count == 0) {
			components[typeName] = new List<KeyValuePair<string, string>>();
		}
	}

	private static string InferType(string fieldName, JsonElement value,
		Dictionary<string, List<KeyValuePair<string, string>>> components,
		HashSet<string> processed) {

		switch (value.ValueKind) {
			case JsonValueKind.Number:
				if (value.TryGetInt32(out _)) return "int";
				if (value.TryGetInt64(out _)) return "long";
				return "float";
			case JsonValueKind.String:
				return "string";
			case JsonValueKind.True:
			case JsonValueKind.False:
				return "bool";
			case JsonValueKind.Array:
				if (value.GetArrayLength() == 0) return "string[]";
				return InferType(fieldName, value[0], components, processed) + "[]";
			case JsonValueKind.Object:
				var nestedName = fieldName;
				if (!processed.Contains(nestedName)) {
					var nestedFields = new List<KeyValuePair<string, string>>();
					ExtractFields(nestedName, value, nestedFields, components, processed);
					if (nestedFields.Count > 0) {
						components[nestedName] = nestedFields;
						processed.Add(nestedName);
					}
				}
				return nestedName;
			default:
				return "string";
		}
	}

	private static SourceConfig? MatchConfig(string filePath, ImmutableArray<SourceConfig> configs) {
		foreach (var c in configs)
			if (!string.IsNullOrEmpty(c.SourcePath) && filePath.Contains(c.SourcePath))
				return c;
		return null;
	}

	private static void EmitComponentStructs(SourceProductionContext spc,
		Dictionary<string, List<KeyValuePair<string, string>>> components,
		SourceConfig config) {

		var ns = string.IsNullOrEmpty(config.Namespace) ? "DataCatalyst.Generated" : config.Namespace;

		var members = new List<MemberDeclarationSyntax>();
		foreach (var kv in components.OrderBy(c => c.Key)) {
			var structMembers = new List<MemberDeclarationSyntax>();
			foreach (var field in kv.Value) {
				structMembers.Add(
					FieldDeclaration(
						VariableDeclaration(ParseTypeName(field.Value))
							.WithVariables(SingletonSeparatedList(
								VariableDeclarator(Identifier(field.Key)))))
						.WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword))));
			}

			var attrs = new List<AttributeSyntax>();
			attrs.Add(Attribute(ParseName("DataCatalyst.Abstractions.DataComponent")));

			foreach (var an in config.Attributes) {
				attrs.Add(Attribute(ParseName(an)));
			}

			members.Add(
				StructDeclaration(kv.Key)
					.WithModifiers(TokenList(
						Token(SyntaxKind.PublicKeyword),
						Token(SyntaxKind.PartialKeyword)))
					.WithAttributeLists(SingletonList(
						AttributeList(SeparatedList(attrs))))
					.AddMembers(structMembers.ToArray()));
		}

		var cu = CompilationUnit()
			.WithLeadingTrivia(Comment("// <auto-generated/>\n#nullable enable"));

		cu = cu.AddMembers(
				NamespaceDeclaration(IdentifierName(ns))
					.AddMembers(members.ToArray()))
			.NormalizeWhitespace(eol: "\n");

		spc.AddSource("JsonComponents.g.cs",
			SourceText.From(cu.ToFullString(), Encoding.UTF8));
	}

	private static void EmitEntryConstants(SourceProductionContext spc,
		Dictionary<string, string> entryConcepts,
		List<string> allEntryKeys) {

		var conceptGroups = new Dictionary<string, List<string>>();
		foreach (var kv in entryConcepts) {
			if (!conceptGroups.TryGetValue(kv.Value, out var list)) {
				list = new List<string>();
				conceptGroups[kv.Value] = list;
			}
			list.Add(kv.Key);
		}

		var assigned = new HashSet<string>();
		foreach (var list in conceptGroups.Values)
			foreach (var e in list)
				assigned.Add(e);

		var unassigned = new List<string>();
		foreach (var e in allEntryKeys)
			if (!assigned.Contains(e))
				unassigned.Add(e);

		if (unassigned.Count > 0) {
			foreach (var entry in unassigned) {
				spc.ReportDiagnostic(Diagnostic.Create(MissingConceptError, Location.None, entry));
			}
			return;
		}

		if (conceptGroups.Count == 0) return;

		var allEntryIdsSorted = allEntryKeys.OrderBy(n => n).ToList();
		var keyFields = new List<MemberDeclarationSyntax>();
		for (int i = 0; i < allEntryIdsSorted.Count; i++) {
			keyFields.Add(
				FieldDeclaration(
					VariableDeclaration(PredefinedType(Token(SyntaxKind.IntKeyword)))
						.WithVariables(SingletonSeparatedList(
							VariableDeclarator(Identifier(allEntryIdsSorted[i]))
								.WithInitializer(EqualsValueClause(
									LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i)))))))
					.WithModifiers(TokenList(
						Token(SyntaxKind.PublicKeyword),
						Token(SyntaxKind.ConstKeyword))));
		}

		var members = new List<MemberDeclarationSyntax>();
		members.Add(
			ClassDeclaration("Keys")
				.WithModifiers(TokenList(
					Token(SyntaxKind.PublicKeyword),
					Token(SyntaxKind.StaticKeyword)))
				.AddMembers(keyFields.ToArray()));

		var cu = CompilationUnit()
			.WithLeadingTrivia(Comment("// <auto-generated/>\n#nullable enable"))
			.AddMembers(
				NamespaceDeclaration(IdentifierName("DataCatalyst.Generated"))
					.AddMembers(members.ToArray()))
			.NormalizeWhitespace(eol: "\n");

		spc.AddSource("JsonEntries.g.cs",
			SourceText.From(cu.ToFullString(), Encoding.UTF8));
	}

		private static void EmitRegistrations(SourceProductionContext spc,
			Dictionary<string, List<KeyValuePair<string, string>>> components,
			Dictionary<string, string> entryConcepts) {

			var initBody = new List<StatementSyntax>();
			var regBody = new List<StatementSyntax>();

			foreach (var typeName in components.Keys.OrderBy(k => k)) {
				var fullType = "global::DataCatalyst.Generated." + typeName;

				initBody.Add(ExpressionStatement(
					InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							ParseExpression("global::DataCatalyst.Core.PrimitiveRegistry.Default"),
							GenericName("Register")
								.WithTypeArgumentList(TypeArgumentList(
									SingletonSeparatedList(ParseTypeName(fullType))))))));

				regBody.Add(ExpressionStatement(
					InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("registry"),
							GenericName("RegisterComponent")
								.WithTypeArgumentList(TypeArgumentList(
									SingletonSeparatedList(ParseTypeName(fullType))))))));
			}

			var cu = CompilationUnit()
			.WithLeadingTrivia(Comment("// <auto-generated/>\n#nullable enable"))
			.AddMembers(
				NamespaceDeclaration(IdentifierName("DataCatalyst.Generated"))
					.AddMembers(
						ClassDeclaration("JsonComponentRegistrations")
							.WithModifiers(TokenList(
								Token(SyntaxKind.InternalKeyword),
								Token(SyntaxKind.StaticKeyword),
								Token(SyntaxKind.PartialKeyword)))
							.AddMembers(
								MethodDeclaration(
										PredefinedType(Token(SyntaxKind.VoidKeyword)),
										Identifier("Init"))
									.WithAttributeLists(SingletonList(
										AttributeList(SingletonSeparatedList(
											Attribute(ParseName("System.Runtime.CompilerServices.ModuleInitializer"))))))
									.WithModifiers(TokenList(
										Token(SyntaxKind.InternalKeyword),
										Token(SyntaxKind.StaticKeyword)))
									.WithBody(Block(initBody)),
								MethodDeclaration(
										PredefinedType(Token(SyntaxKind.VoidKeyword)),
										Identifier("RegisterTo"))
									.WithModifiers(TokenList(
										Token(SyntaxKind.PublicKeyword),
										Token(SyntaxKind.StaticKeyword)))
									.WithParameterList(ParameterList(
										SingletonSeparatedList(
											Parameter(Identifier("registry"))
												.WithType(ParseTypeName("global::DataCatalyst.Core.DataRegistry")))))
									.WithBody(Block(regBody)))))
				.NormalizeWhitespace(eol: "\n");

		spc.AddSource("JsonComponentRegistrations.g.cs",
			SourceText.From(cu.ToFullString(), Encoding.UTF8));
	}

	private static void EmitMerge(SourceProductionContext spc,
		Dictionary<string, List<KeyValuePair<string, string>>> components) {

		var initBody = new List<StatementSyntax>();

		foreach (var kv in components) {
			var fullType = "global::DataCatalyst.Generated." + kv.Key;

			var stmts = new List<StatementSyntax>();

			stmts.Add(LocalDeclarationStatement(
				VariableDeclaration(
					IdentifierName("var"),
					SingletonSeparatedList(
						VariableDeclarator(Identifier("pVal"))
							.WithInitializer(EqualsValueClause(
								CastExpression(ParseTypeName(fullType), IdentifierName("current"))))))));

			stmts.Add(LocalDeclarationStatement(
				VariableDeclaration(
					IdentifierName("var"),
					SingletonSeparatedList(
						VariableDeclarator(Identifier("cVal"))
							.WithInitializer(EqualsValueClause(
								CastExpression(ParseTypeName(fullType), IdentifierName("inherited"))))))));

			foreach (var field in kv.Value) {
				var eqComparer = ParseExpression(
					"global::System.Collections.Generic.EqualityComparer<" + field.Value + ">");

				var equalsCall = InvocationExpression(
					MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							eqComparer,
							IdentifierName("Default")),
						IdentifierName("Equals")),
					ArgumentList(SeparatedList(new[] {
						Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("pVal"), IdentifierName(field.Key))),
						Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("cVal"), IdentifierName(field.Key)))
					})));

				var notEqual = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, equalsCall);

				var ifStmt = IfStatement(notEqual,
					ExpressionStatement(
						AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
							MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
								IdentifierName("pVal"), IdentifierName(field.Key)),
							MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
								IdentifierName("cVal"), IdentifierName(field.Key)))));

				stmts.Add(ifStmt);
			}

			stmts.Add(ReturnStatement(IdentifierName("pVal")));

			var body = Block(stmts);

			var registerMethod = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
				ParseExpression("global::DataCatalyst.Core.ComponentMerger"),
				GenericName("Register")
					.WithTypeArgumentList(TypeArgumentList(
						SingletonSeparatedList(ParseTypeName(fullType)))));

			var lambda = ParenthesizedLambdaExpression(
				ParameterList(SeparatedList(new[] {
					Parameter(Identifier("current")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
					Parameter(Identifier("inherited")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))
				})),
				body);

			initBody.Add(ExpressionStatement(
				InvocationExpression(registerMethod)
					.WithArgumentList(ArgumentList(
						SingletonSeparatedList(Argument(lambda))))));
		}

		var cu = CompilationUnit()
			.WithLeadingTrivia(Comment("// <auto-generated/>\n#nullable enable"))
			.AddMembers(
				NamespaceDeclaration(IdentifierName("DataCatalyst.Generated"))
					.AddMembers(
						ClassDeclaration("JsonComponentMergeRegistrations")
							.WithModifiers(TokenList(
								Token(SyntaxKind.InternalKeyword),
								Token(SyntaxKind.StaticKeyword)))
							.AddMembers(
								MethodDeclaration(
										PredefinedType(Token(SyntaxKind.VoidKeyword)),
										Identifier("Init"))
									.WithAttributeLists(SingletonList(
										AttributeList(SingletonSeparatedList(
											Attribute(ParseName("System.Runtime.CompilerServices.ModuleInitializer"))))))
									.WithModifiers(TokenList(
										Token(SyntaxKind.InternalKeyword),
										Token(SyntaxKind.StaticKeyword)))
									.WithBody(Block(initBody)))))
				.NormalizeWhitespace(eol: "\n");

		spc.AddSource("JsonComponentMergeRegistrations.g.cs",
			SourceText.From(cu.ToFullString(), Encoding.UTF8));
	}
}
