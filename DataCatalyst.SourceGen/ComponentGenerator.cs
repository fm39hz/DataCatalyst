namespace DataCatalyst;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

[Generator]
public sealed class ComponentGenerator : IIncrementalGenerator {
	private static readonly DiagnosticDescriptor StructRequiredError = new(
		id: "DC001",
		title: "DataComponent must be a struct",
		messageFormat: "[DataComponent] on '{0}' is only valid on struct types",
		category: "DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor CollisionWarning = new(
		id: "DC002",
		title: "Discriminator collision",
		messageFormat: "[DataComponent] types with short name '{0}' collide. Resolved discriminators: '{1}'. Use these fully-qualified strings in JSON.",
		category: "DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	private const string DataComponentAttr = "DataCatalyst.Abstractions.DataComponentAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		var primitives = context.SyntaxProvider.ForAttributeWithMetadataName(
			DataComponentAttr,
			static (node, _) => node is TypeDeclarationSyntax,
			static (ctx, _) => {
				var t = (INamedTypeSymbol)ctx.TargetSymbol;
				var errorLoc = t.TypeKind != TypeKind.Structure
						? ctx.TargetNode.GetLocation()
						: null;
				var fullType = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var shortName = t.Name;
				var nsQualified = fullType.StartsWith("global::") ? fullType.Substring(8) : fullType;
				nsQualified = nsQualified.Replace('+', '.');

				var fields = t.GetMembers().OfType<IFieldSymbol>()
					.Where(f => f.DeclaredAccessibility == Accessibility.Public)
					.Select(f => f.Name + ":" + f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
					.ToArray();

				return new ComponentResult(fullType, shortName, nsQualified, errorLoc, fields);
			}).Collect();

		var configs = ConfigHelper.GetConfigs(context);
		var compilation = context.CompilationProvider;
		var combined = primitives.Combine(configs).Combine(compilation);

		context.RegisterSourceOutput(combined,
			static (spc, data) => {
				var (primsAndConfigs, comp) = data;
				var (pr, configs) = primsAndConfigs;

				foreach (var p in pr) {
					if (p.ErrorLocation is { } loc) {
						spc.ReportDiagnostic(Diagnostic.Create(StructRequiredError, loc));
					}
				}

				var cfg = configs.Length > 0 ? configs[0] : new SourceConfig("", "DataCatalyst.Generated", new List<string>());
				Emit(spc, pr, cfg, comp);
				EmitMerge(spc, pr, cfg);
			});
	}

	private readonly struct ComponentResult(string? fullType, string? shortName, string? nsQualified, Location? errorLocation, string[]? fields) {
		public readonly string? FullType = fullType;
		public readonly string? ShortName = shortName;
		public readonly string? NsQualified = nsQualified;
		public readonly Location? ErrorLocation = errorLocation;
		public readonly string[]? Fields = fields;
	}

	private static void Emit(SourceProductionContext spc,
		ImmutableArray<ComponentResult> allPrims,
		SourceConfig config,
		Compilation compilation) {

		var counts = new Dictionary<string, int>();
		var shortToNs = new Dictionary<string, List<string>>();

		foreach (var p in allPrims) {
			if (p.FullType == null || p.ShortName == null || p.NsQualified == null) continue;
			counts.TryGetValue(p.ShortName, out var c);
			counts[p.ShortName] = c + 1;
			if (!shortToNs.ContainsKey(p.ShortName))
				shortToNs[p.ShortName] = new List<string>();
			shortToNs[p.ShortName].Add(p.NsQualified);
		}

		var prims = new List<(string FullType, string Discrim)>();
		foreach (var p in allPrims) {
			if (p.FullType == null || p.NsQualified == null || p.ShortName == null) continue;
			var finalD = counts[p.ShortName] > 1 ? p.NsQualified : p.ShortName;
			prims.Add((p.FullType, finalD));
		}

		foreach (var kv in counts) {
			if (kv.Value <= 1) continue;
			var msg = shortToNs.TryGetValue(kv.Key, out var list) ? string.Join(", ", list) : "";
			spc.ReportDiagnostic(Diagnostic.Create(CollisionWarning, Location.None, kv.Key, msg));
		}

		if (prims.Count == 0) return;

		var initBody = new List<StatementSyntax>();
		foreach (var (ft, _) in prims) {
			initBody.Add(BuildRegisterCall("global::DataCatalyst.Core.PrimitiveRegistry.Default", ft));
		}
		initBody.Add(BuildRegisterIdsStatement(prims));

		EmitRegistrations(spc, prims, initBody, config);
		EmitAotContexts(spc, prims, config, compilation);
	}

	private static void EmitAotContexts(SourceProductionContext spc,
		List<(string FullType, string Discrim)> prims,
		SourceConfig config,
		Compilation compilation) {

		var attributes = new List<AttributeData>();

		// Check current assembly
		foreach (var attr in compilation.Assembly.GetAttributes()) {
			if (attr.AttributeClass?.ToDisplayString() == "DataCatalyst.Abstractions.AotContextAttribute") {
				attributes.Add(attr);
			}
		}

		// Check referenced assemblies
		foreach (var refAssembly in compilation.SourceModule.ReferencedAssemblySymbols) {
			foreach (var attr in refAssembly.GetAttributes()) {
				if (attr.AttributeClass?.ToDisplayString() == "DataCatalyst.Abstractions.AotContextAttribute") {
					attributes.Add(attr);
				}
			}
		}

		if (attributes.Count == 0) return;

		// Collect all component types from current and referenced assemblies
		var allComponents = new List<string>();
		foreach (var (ft, _) in prims) {
			allComponents.Add(ft);
		}

		foreach (var refAssembly in compilation.SourceModule.ReferencedAssemblySymbols) {
			var name = refAssembly.Name;
			if (name.StartsWith("System") || name.StartsWith("Microsoft") || name.StartsWith("mscorlib") || name.StartsWith("netstandard")) {
				continue;
			}
			RegisterComponentsFromNamespace(refAssembly.GlobalNamespace, allComponents);
		}

		var uniqueComponents = allComponents.Distinct().ToList();

		var hasModuleInitializer = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") != null;

		foreach (var attr in attributes) {
			if (attr.ConstructorArguments.Length < 4) continue;
			var contextName = attr.ConstructorArguments[0].Value as string;
			var baseType = attr.ConstructorArguments[1].Value as string;
			var attributeType = attr.ConstructorArguments[2].Value as string;
			var registerMethod = attr.ConstructorArguments[3].Value as string;

			if (string.IsNullOrEmpty(contextName) || string.IsNullOrEmpty(baseType) ||
				string.IsNullOrEmpty(attributeType) || string.IsNullOrEmpty(registerMethod)) {
				continue;
			}

			// Check if the user has declared this context class in their assembly
			var ns = config.Namespace ?? "DataCatalyst.Generated";
			var fullContextName = $"{ns}.{contextName}";
			var contextSymbol = compilation.GetTypeByMetadataName(fullContextName);
			if (contextSymbol == null) continue;

			var extraAttributes = new List<string>();
			foreach (var namedArg in attr.NamedArguments) {
				if (namedArg.Key == "ExtraClassAttributes" && namedArg.Value.Kind == TypedConstantKind.Array) {
					foreach (var val in namedArg.Value.Values) {
						if (val.Value is string s) {
							extraAttributes.Add(s);
						}
					}
				}
			}

			var sb = new StringBuilder();
			sb.AppendLine("// <auto-generated/>");
			sb.AppendLine("#nullable enable");
			sb.AppendLine($"namespace {ns};");
			sb.AppendLine();

			foreach (var extra in extraAttributes) {
				sb.AppendLine($"[{extra}]");
			}

			foreach (var ft in uniqueComponents) {
				sb.AppendLine($"[{attributeType}(typeof({ft}))]");
			}

			sb.AppendLine($"internal partial class {contextName} {{}}");
			sb.AppendLine();
			sb.AppendLine($"internal static class {contextName}Registration_Code {{");
			sb.AppendLine("	[System.Runtime.CompilerServices.ModuleInitializer]");
			sb.AppendLine("	internal static void Init() {");
			sb.AppendLine($"		{registerMethod}({contextName}.Default);");
			sb.AppendLine("	}");
			sb.AppendLine("}");
			sb.AppendLine();

			if (!hasModuleInitializer) {
				sb.AppendLine("namespace System.Runtime.CompilerServices");
				sb.AppendLine("{");
				sb.AppendLine("	[System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]");
				sb.AppendLine("	internal sealed class ModuleInitializerAttribute : System.Attribute { }");
				sb.AppendLine("}");
			}

			spc.AddSource($"{contextName}_Code.g.cs", sb.ToString());
		}
	}

	private static void RegisterComponentsFromNamespace(INamespaceSymbol ns, List<string> allComponents) {
		foreach (var member in ns.GetMembers()) {
			if (member is INamespaceSymbol nestedNs) {
				RegisterComponentsFromNamespace(nestedNs, allComponents);
			} else if (member is INamedTypeSymbol typeSymbol) {
				if (typeSymbol.TypeKind == TypeKind.Structure && HasDataComponentAttribute(typeSymbol)) {
					var fullType = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
					allComponents.Add(fullType);
				}
				RegisterNestedComponents(typeSymbol, allComponents);
			}
		}
	}

	private static void RegisterNestedComponents(INamedTypeSymbol typeSymbol, List<string> allComponents) {
		foreach (var nested in typeSymbol.GetTypeMembers()) {
			if (nested.TypeKind == TypeKind.Structure && HasDataComponentAttribute(nested)) {
				var fullType = nested.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				allComponents.Add(fullType);
			}
			RegisterNestedComponents(nested, allComponents);
		}
	}

	private static bool HasDataComponentAttribute(INamedTypeSymbol type) {
		foreach (var attr in type.GetAttributes()) {
			if (attr.AttributeClass?.ToDisplayString() == "DataCatalyst.Abstractions.DataComponentAttribute") {
				return true;
			}
		}
		return false;
	}

	private static void EmitRegistrations(SourceProductionContext spc,
		List<(string FullType, string Discrim)> prims, List<StatementSyntax> initBody,
		SourceConfig config) {

		var regBody = new List<StatementSyntax>();
		foreach (var (ft, _) in prims) {
			regBody.Add(BuildGenericCall("registry", "RegisterComponent", ft));
		}

		var ns = string.IsNullOrEmpty(config.Namespace) ? "DataCatalyst.Generated" : config.Namespace;
		var cu = CompilationUnit()
			.WithLeadingTrivia(Comment("// <auto-generated/>\n#nullable enable"));

		cu = cu.AddMembers(
				NamespaceDeclaration(IdentifierName(ns))
					.AddMembers(
						ClassDeclaration("ComponentRegistrations")
							.WithModifiers(TokenList(
								Token(SyntaxKind.PublicKeyword),
								Token(SyntaxKind.StaticKeyword),
								Token(SyntaxKind.PartialKeyword)))
							.AddMembers(
								MethodDeclaration(
										PredefinedType(Token(SyntaxKind.VoidKeyword)),
										Identifier("Init"))
									.WithAttributeLists(
										SingletonList(
											AttributeList(
												SingletonSeparatedList(
													Attribute(
														ParseName("System.Runtime.CompilerServices.ModuleInitializer"))))))
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
									.WithParameterList(
										ParameterList(
											SingletonSeparatedList(
												Parameter(Identifier("registry"))
													.WithType(
														ParseTypeName("global::DataCatalyst.Core.DataRegistry")))))
									.WithBody(Block(regBody))))
					)
			.NormalizeWhitespace();

		spc.AddSource("ComponentRegistrations.g.cs",
			SourceText.From(cu.ToFullString(), Encoding.UTF8));
	}

	private static void EmitMerge(SourceProductionContext spc,
		ImmutableArray<ComponentResult> allPrims,
		SourceConfig config) {

		var valid = allPrims.Where(p => p.FullType != null && p.Fields != null && p.Fields.Length > 0).ToList();
		if (valid.Count == 0) return;

		var initBody = new List<StatementSyntax>();

		foreach (var p in valid) {
			var fullType = ParseTypeName(p.FullType!);

			var pVarDecl = LocalDeclarationStatement(
				VariableDeclaration(IdentifierName("var"))
					.WithVariables(
						SingletonSeparatedList(
							VariableDeclarator(Identifier("pVal"))
								.WithInitializer(
									EqualsValueClause(
										CastExpression(fullType, IdentifierName("current")))))));

			var cVarDecl = LocalDeclarationStatement(
				VariableDeclaration(IdentifierName("var"))
					.WithVariables(
						SingletonSeparatedList(
							VariableDeclarator(Identifier("cVal"))
								.WithInitializer(
									EqualsValueClause(
										CastExpression(fullType, IdentifierName("inherited")))))));

			var fieldStatements = new List<StatementSyntax>();
			foreach (var field in p.Fields!) {
				var colonIdx = field.LastIndexOf(':');
				if (colonIdx < 0) continue;
				var fieldName = field.Substring(0, colonIdx);
				var fieldTypeStr = field.Substring(colonIdx + 1);

				var equalsExpr = ParseExpression(
					$"global::System.Collections.Generic.EqualityComparer<{fieldTypeStr}>.Default.Equals(pVal.{fieldName}, default({fieldTypeStr}))");

				var ifStmt = IfStatement(
					equalsExpr,
					ExpressionStatement(
						AssignmentExpression(
							SyntaxKind.SimpleAssignmentExpression,
							MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								IdentifierName("pVal"),
								IdentifierName(fieldName)),
							MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								IdentifierName("cVal"),
								IdentifierName(fieldName)))));

				fieldStatements.Add(ifStmt);
			}

			var returnStmt = ReturnStatement(IdentifierName("pVal"));

			var bodyStatements = new List<StatementSyntax> { pVarDecl, cVarDecl };
			bodyStatements.AddRange(fieldStatements);
			bodyStatements.Add(returnStmt);

			initBody.Add(
				ExpressionStatement(
					InvocationExpression(
						MemberAccessExpression(
							SyntaxKind.SimpleMemberAccessExpression,
							ParseExpression("global::DataCatalyst.Core.ComponentMerger"),
							GenericName("Register")
								.WithTypeArgumentList(
									TypeArgumentList(
										SingletonSeparatedList(fullType)))))
						.WithArgumentList(
							ArgumentList(
								SingletonSeparatedList(
									Argument(
										ParenthesizedLambdaExpression(
											ParameterList(
												SeparatedList(new[] {
													Parameter(Identifier("current"))
														.WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
													Parameter(Identifier("inherited"))
														.WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))
												})),
											Block(bodyStatements))))))));
		}

		var ns = string.IsNullOrEmpty(config.Namespace) ? "DataCatalyst.Generated" : config.Namespace;
		var cu = CompilationUnit()
			.WithLeadingTrivia(Comment("// <auto-generated/>\n#nullable enable"));

		cu = cu.AddMembers(
				NamespaceDeclaration(IdentifierName(ns))
					.AddMembers(
						ClassDeclaration("ComponentMergeRegistrations")
							.WithModifiers(TokenList(
								Token(SyntaxKind.InternalKeyword),
								Token(SyntaxKind.StaticKeyword)))
							.AddMembers(
								MethodDeclaration(
										PredefinedType(Token(SyntaxKind.VoidKeyword)),
										Identifier("Init"))
									.WithAttributeLists(
										SingletonList(
											AttributeList(
												SingletonSeparatedList(
													Attribute(
														ParseName("System.Runtime.CompilerServices.ModuleInitializer"))))))
									.WithModifiers(TokenList(
										Token(SyntaxKind.InternalKeyword),
										Token(SyntaxKind.StaticKeyword)))
									.WithBody(Block(initBody)))))
			.NormalizeWhitespace(eol: "\n");

		spc.AddSource("ComponentMergeRegistrations.g.cs",
			Microsoft.CodeAnalysis.Text.SourceText.From(cu.ToFullString(), Encoding.UTF8));
	}

	private static string GetShortName(string fullType) {
		var name = fullType;
		if (name.StartsWith("global::"))
			name = name.Substring(8);
		var lastDot = name.LastIndexOf('.');
		return lastDot >= 0 ? name.Substring(lastDot + 1) : name;
	}

	private static StatementSyntax BuildRegisterCall(string target, string fullType) => ExpressionStatement(
			InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseExpression(target),
					GenericName("Register")
						.WithTypeArgumentList(
							TypeArgumentList(
								SingletonSeparatedList(ParseTypeName(fullType)))))));

	private static StatementSyntax BuildGenericCall(string instance, string method, string fullType) => ExpressionStatement(
			InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseExpression(instance),
					GenericName(method)
						.WithTypeArgumentList(
							TypeArgumentList(
								SingletonSeparatedList(ParseTypeName(fullType)))))));

	private static StatementSyntax BuildRegisterIdsStatement(List<(string FullType, string Discrim)> prims) {
		var elems = new List<ExpressionSyntax>();
		foreach (var (ft, d) in prims) {
			elems.Add(
				InitializerExpression(
					SyntaxKind.ComplexElementInitializerExpression,
					SeparatedList<ExpressionSyntax>(
						new List<ExpressionSyntax> {
							LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(d)),
							TypeOfExpression(ParseTypeName(ft))
						})));
		}

		return ExpressionStatement(
			InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseExpression("global::DataCatalyst.Core.PrimitiveRegistry.Default"),
					IdentifierName("RegisterIds")))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(
								ObjectCreationExpression(
									ParseTypeName("global::System.Collections.Generic.Dictionary<string, global::System.Type>"))
									.WithInitializer(
										InitializerExpression(
											SyntaxKind.CollectionInitializerExpression,
											SeparatedList(elems))))))));
	}
}
