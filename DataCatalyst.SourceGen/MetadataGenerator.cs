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

/// <summary>
/// Generates [DataComponent] structs and metadata from JSON data files.
/// Data is the Single Source of Truth — no manual C# code needed.
/// </summary>
[Generator]
public sealed class MetadataGenerator : IIncrementalGenerator {

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

		context.RegisterSourceOutput(jsonFiles, static (spc, files) => {
			var components = new Dictionary<string, List<KeyValuePair<string, string>>>();
			var conceptEntries = new Dictionary<string, List<string>>();
			var allEntryKeys = new List<string>();
			var processed = new HashSet<string>();

			foreach (var file in files) {
				ProcessJson(file.Text, file.FileName, components, conceptEntries, allEntryKeys, processed);
			}

			if (components.Count == 0) return;

			WriteMetadataFile(files, components, conceptEntries, allEntryKeys);
			EmitComponentStructs(spc, components);
			EmitConceptAndKeyConstants(spc, conceptEntries, allEntryKeys);
			EmitRegistrations(spc, components);
			EmitMerge(spc, components);
		});
	}

	private static void ProcessJson(string json, string fileName,
		Dictionary<string, List<KeyValuePair<string, string>>> components,
		Dictionary<string, List<string>> conceptEntries,
		List<string> allEntryKeys,
		HashSet<string> processed) {

		try {
			using var doc = JsonDocument.Parse(json);
			string? conceptName = null;
			var entryComponents = new List<string>();

			foreach (var prop in doc.RootElement.EnumerateObject()) {
				if (string.Equals(prop.Name, "Concept", StringComparison.Ordinal) ||
					string.Equals(prop.Name, "concept", StringComparison.Ordinal)) {
					conceptName = prop.Value.GetString();
					continue;
				}
				if (string.Equals(prop.Name, "inherits", StringComparison.OrdinalIgnoreCase)) continue;

				if (prop.Value.ValueKind == JsonValueKind.Object) {
					if (!processed.Contains(prop.Name)) {
						var fields = new List<KeyValuePair<string, string>>();
						ExtractFields(prop.Name, prop.Value, fields, components, processed);
						if (fields.Count > 0) {
							components[prop.Name] = fields;
							processed.Add(prop.Name);
						}
					}
					entryComponents.Add(prop.Name);
				}
			}

			if (entryComponents.Count > 0) {
				allEntryKeys.Add(fileName);
				if (conceptName != null) {
					if (!conceptEntries.ContainsKey(conceptName))
						conceptEntries[conceptName] = new List<string>();
					conceptEntries[conceptName].Add(fileName);
				}
			}
		}
		catch { }
	}

	private static void ExtractFields(string typeName, JsonElement obj,
		List<KeyValuePair<string, string>> fields,
		Dictionary<string, List<KeyValuePair<string, string>>> components,
		HashSet<string> processed) {

		foreach (var prop in obj.EnumerateObject()) {
			var fieldType = InferType(prop.Name, prop.Value, components, processed);
			fields.Add(new KeyValuePair<string, string>(prop.Name, fieldType));
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

	private static void WriteMetadataFile(ImmutableArray<(string FileName, string Text, string Dir)> files,
		Dictionary<string, List<KeyValuePair<string, string>>> components,
		Dictionary<string, List<string>> conceptEntries,
		List<string> allEntryKeys) {

		if (files.Length == 0) return;
		var dir = Path.Combine(files[0].Dir, ".datacatalyst");
		Directory.CreateDirectory(dir);

		var sb = new StringBuilder();
		sb.AppendLine("{");
		sb.AppendLine("  \"components\": {");

		bool firstComp = true;
		foreach (var kv in components.OrderBy(c => c.Key)) {
			if (!firstComp) sb.AppendLine(",");
			firstComp = false;
			sb.AppendFormat("    \"{0}\": {{", kv.Key);
			bool firstField = true;
			foreach (var field in kv.Value) {
				if (!firstField) sb.Append(",");
				firstField = false;
				sb.AppendFormat("\"{0}\":\"{1}\"", field.Key, field.Value);
			}
			sb.Append("}");
		}
		if (components.Count > 0) sb.AppendLine();
		sb.AppendLine("  },");
		sb.AppendLine("  \"entries\": {");

		bool firstEntry = true;
		foreach (var key in allEntryKeys.OrderBy(k => k)) {
			if (!firstEntry) sb.AppendLine(",");
			firstEntry = false;
			string? concept = null;
			foreach (var ck in conceptEntries) {
				if (ck.Value.Contains(key)) { concept = ck.Key; break; }
			}
			sb.AppendFormat("    \"{0}\": {{\"concept\":{1}}}", key,
				concept != null ? "\"" + concept + "\"" : "null");
		}
		if (allEntryKeys.Count > 0) sb.AppendLine();
		sb.AppendLine("  }");
		sb.AppendLine("}");

		File.WriteAllText(Path.Combine(dir, "metadata.json"), sb.ToString());
	}

	private static void EmitComponentStructs(SourceProductionContext spc,
		Dictionary<string, List<KeyValuePair<string, string>>> components) {

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

			members.Add(
				StructDeclaration(kv.Key)
					.WithModifiers(TokenList(
						Token(SyntaxKind.PublicKeyword),
						Token(SyntaxKind.ReadOnlyKeyword),
						Token(SyntaxKind.PartialKeyword)))
					.WithAttributeLists(SingletonList(
						AttributeList(SingletonSeparatedList(
							Attribute(ParseName("DataComponent"))))))
					.AddMembers(structMembers.ToArray()));
		}

		var cu = CompilationUnit()
			.WithLeadingTrivia(Comment("// <auto-generated/>\n#nullable enable"))
			.AddMembers(
				NamespaceDeclaration(IdentifierName("DataCatalyst.Generated"))
					.AddMembers(members.ToArray()))
			.NormalizeWhitespace(eol: "\n");

		spc.AddSource("JsonComponents.g.cs",
			SourceText.From(cu.ToFullString(), Encoding.UTF8));
	}

	private static void EmitConceptAndKeyConstants(SourceProductionContext spc,
		Dictionary<string, List<string>> conceptEntries,
		List<string> allEntryKeys) {

		var members = new List<MemberDeclarationSyntax>();

		if (conceptEntries.Count > 0) {
			var conceptMembers = new List<MemberDeclarationSyntax>();
			foreach (var kv in conceptEntries.OrderBy(c => c.Key)) {
				var sortedEntries = kv.Value.OrderBy(n => n).ToList();
				var fields = new List<MemberDeclarationSyntax>();
				for (int i = 0; i < sortedEntries.Count; i++) {
					fields.Add(
						FieldDeclaration(
							VariableDeclaration(PredefinedType(Token(SyntaxKind.IntKeyword)))
								.WithVariables(SingletonSeparatedList(
									VariableDeclarator(Identifier(sortedEntries[i]))
										.WithInitializer(EqualsValueClause(
											LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i)))))))
						.WithModifiers(TokenList(
							Token(SyntaxKind.PublicKeyword),
							Token(SyntaxKind.ConstKeyword))));
				}

				conceptMembers.Add(
					StructDeclaration(kv.Key)
						.WithModifiers(TokenList(
							Token(SyntaxKind.PublicKeyword),
							Token(SyntaxKind.ReadOnlyKeyword),
							Token(SyntaxKind.PartialKeyword)))
						.WithAttributeLists(SingletonList(
							AttributeList(SingletonSeparatedList(
								Attribute(ParseName("DataConcept"),
									AttributeArgumentList(SingletonSeparatedList(
										AttributeArgument(
											LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(kv.Key))))))))))
						.AddMembers(fields.ToArray()));
			}

			members.Add(
				ClassDeclaration("Concept")
					.WithModifiers(TokenList(
						Token(SyntaxKind.PublicKeyword),
						Token(SyntaxKind.StaticKeyword),
						Token(SyntaxKind.PartialKeyword)))
					.AddMembers(conceptMembers.ToArray()));
		}

		var sortedKeys = allEntryKeys.OrderBy(n => n).ToList();
		if (sortedKeys.Count > 0) {
			var keyFields = new List<MemberDeclarationSyntax>();
			for (int i = 0; i < sortedKeys.Count; i++) {
				keyFields.Add(
					FieldDeclaration(
						VariableDeclaration(PredefinedType(Token(SyntaxKind.IntKeyword)))
							.WithVariables(SingletonSeparatedList(
								VariableDeclarator(Identifier(sortedKeys[i]))
									.WithInitializer(EqualsValueClause(
										LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i)))))))
					.WithModifiers(TokenList(
						Token(SyntaxKind.PublicKeyword),
						Token(SyntaxKind.ConstKeyword))));
			}

			members.Add(
				ClassDeclaration("Keys")
					.WithModifiers(TokenList(
						Token(SyntaxKind.PublicKeyword),
						Token(SyntaxKind.StaticKeyword)))
					.AddMembers(keyFields.ToArray()));
		}

		if (members.Count == 0) return;

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
		Dictionary<string, List<KeyValuePair<string, string>>> components) {

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
			var sb = new StringBuilder();
			sb.AppendLine("var pVal = (" + fullType + ")current;");
			sb.AppendLine("var cVal = (" + fullType + ")inherited;");
			foreach (var field in kv.Value) {
				sb.AppendLine("if (!global::System.Collections.Generic.EqualityComparer<" + field.Value +
					">.Default.Equals(pVal." + field.Key + ", cVal." + field.Key + ")) pVal." +
					field.Key + " = cVal." + field.Key + ";");
			}
			sb.AppendLine("return pVal;");

			var body = Block(CSharpSyntaxTree.ParseText(sb.ToString()).GetRoot().ChildNodes().Cast<StatementSyntax>().ToArray());

			initBody.Add(ExpressionStatement(
				InvocationExpression(
					MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
						ParseExpression("global::DataCatalyst.Core.ComponentMerger"),
						GenericName("Register")
							.WithTypeArgumentList(TypeArgumentList(
								SingletonSeparatedList(ParseTypeName(fullType))))))
					.WithArgumentList(ArgumentList(
						SingletonSeparatedList(
							Argument(ParenthesizedLambdaExpression(
								ParameterList(SeparatedList(new[] {
									Parameter(Identifier("current")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
									Parameter(Identifier("inherited")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))
								})),
								body)))))));
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
