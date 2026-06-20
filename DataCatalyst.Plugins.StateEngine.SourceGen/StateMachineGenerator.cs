namespace DataCatalyst.Plugins.StateEngine;

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
public sealed class StateMachineGenerator : IIncrementalGenerator {
	private static readonly DiagnosticDescriptor EnumRequiredError = new(
		id: "DC020",
		title: "DataStateEnum/DataSensorEnum must be an enum",
		messageFormat: "[DataStateEnum] and [DataSensorEnum] on '{0}' are only valid on enum types",
		category: "DataCatalyst.StateEngine",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor EmptyEnumWarning = new(
		id: "DC021",
		title: "Enum has no members",
		messageFormat: "[DataStateEnum] or [DataSensorEnum] on '{0}' enum has no members. Mapper will have no valid mappings.",
		category: "DataCatalyst.StateEngine",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	private const string StateEnumAttr = "DataCatalyst.Plugins.StateEngine.Contracts.DataStateEnumAttribute";
	private const string SensorEnumAttr = "DataCatalyst.Plugins.StateEngine.Contracts.DataSensorEnumAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		var stateEnums = context.SyntaxProvider.ForAttributeWithMetadataName(
			StateEnumAttr,
			static (node, _) => node is EnumDeclarationSyntax,
			static (ctx, _) => BuildResult(ctx)).Collect();

		var sensorEnums = context.SyntaxProvider.ForAttributeWithMetadataName(
			SensorEnumAttr,
			static (node, _) => node is EnumDeclarationSyntax,
			static (ctx, _) => BuildResult(ctx)).Collect();

		context.RegisterSourceOutput(stateEnums.Combine(sensorEnums),
			static (spc, combined) => Emit(spc, combined.Left, combined.Right));
	}

	private static EnumResult BuildResult(GeneratorAttributeSyntaxContext ctx) {
		var t = (INamedTypeSymbol)ctx.TargetSymbol;
		var isValid = t.TypeKind == TypeKind.Enum;
		var members = t.GetMembers().OfType<IFieldSymbol>()
			.Where(f => f.ConstantValue != null && !f.Name.StartsWith("value__"))
			.Select(f => f.Name)
			.ToArray();
		var fullType = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var shortName = GetShortName(fullType);
		return new EnumResult(fullType, shortName, isValid, ctx.TargetNode.GetLocation(), members);
	}

	private readonly struct EnumResult {
		public readonly string FullType;
		public readonly string SimpleName;
		public readonly bool IsValid;
		public readonly Location Location;
		public readonly string[] Members;

		public EnumResult(string fullType, string simpleName, bool isValid, Location location, string[] members) {
			FullType = fullType;
			SimpleName = simpleName;
			IsValid = isValid;
			Location = location;
			Members = members;
		}
	}

	private static void Emit(SourceProductionContext spc,
		ImmutableArray<EnumResult> stateResults,
		ImmutableArray<EnumResult> sensorResults) {

		foreach (var r in stateResults) {
			if (!r.IsValid)
				spc.ReportDiagnostic(Diagnostic.Create(EnumRequiredError, r.Location, r.FullType));
			else if (r.Members.Length == 0)
				spc.ReportDiagnostic(Diagnostic.Create(EmptyEnumWarning, r.Location, r.FullType));
		}

		foreach (var r in sensorResults) {
			if (!r.IsValid)
				spc.ReportDiagnostic(Diagnostic.Create(EnumRequiredError, r.Location, r.FullType));
			else if (r.Members.Length == 0)
				spc.ReportDiagnostic(Diagnostic.Create(EmptyEnumWarning, r.Location, r.FullType));
		}

		var validStates = stateResults.Where(r => r.IsValid && r.Members.Length > 0).ToList();
		var validSensors = sensorResults.Where(r => r.IsValid && r.Members.Length > 0).ToList();

		if (validStates.Count == 0 && validSensors.Count == 0) return;

		var initStatements = new List<StatementSyntax>();
		var members = new List<MemberDeclarationSyntax>();

		// Build mapper classes
		foreach (var st in validStates) {
			var cls = BuildMapperClass(st, true);
			members.Add(cls);

			var mapperType = $"{st.SimpleName}StateMapper";
			var interfaceType = ParseTypeName($"global::DataCatalyst.Plugins.StateEngine.Contracts.IStateMapper<{st.FullType}>");
			initStatements.Add(RegisterMapper(interfaceType, mapperType));
		}

		foreach (var se in validSensors) {
			var cls = BuildMapperClass(se, false);
			members.Add(cls);

			var mapperType = $"{se.SimpleName}SensorMapper";
			var interfaceType = ParseTypeName($"global::DataCatalyst.Plugins.StateEngine.Contracts.ISensorMapper<{se.FullType}>");
			initStatements.Add(RegisterMapper(interfaceType, mapperType));
		}

		// Build ModuleInitializer class
		members.Add(
			ClassDeclaration("StateEngineRegistrations")
				.WithModifiers(TokenList(
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
						.WithBody(Block(initStatements))));

		var cu = CompilationUnit()
			.WithLeadingTrivia(Comment("// <auto-generated/>\n#nullable enable"))
			.AddMembers(
				NamespaceDeclaration(IdentifierName("DataCatalyst.Plugins.StateEngine.Generated"))
					.AddMembers(members.ToArray()))
			.NormalizeWhitespace(eol: "\n");

		spc.AddSource("StateMachineRegistrations.g.cs",
			SourceText.From(cu.ToFullString(), Encoding.UTF8));
	}

	private static StatementSyntax RegisterMapper(TypeSyntax interfaceType, string mapperType) {
		return ExpressionStatement(
			InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseExpression("global::DataCatalyst.Core.MapperRegistry.Default"),
					GenericName("Register")
						.WithTypeArgumentList(
							TypeArgumentList(
								SingletonSeparatedList(interfaceType)))))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(
								ObjectCreationExpression(
									ParseTypeName(mapperType))
								.WithArgumentList(ArgumentList()))))));
	}

	private static MemberDeclarationSyntax BuildMapperClass(EnumResult result, bool isState) {
		var className = $"{result.SimpleName}{(isState ? "StateMapper" : "SensorMapper")}";
		var interfacePrefix = isState ? "IStateMapper" : "ISensorMapper";
		var methodName = isState ? "MapState" : "MapSensor";
		var interfaceType = ParseTypeName($"global::DataCatalyst.Plugins.StateEngine.Contracts.{interfacePrefix}<{result.FullType}>");
		var returnType = ParseTypeName(result.FullType);

		// Build switch cases
		var switchArms = new List<SwitchSectionSyntax>();
		foreach (var m in result.Members) {
			switchArms.Add(
				SwitchSection()
					.AddLabels(
						CaseSwitchLabel(
							LiteralExpression(
								SyntaxKind.StringLiteralExpression,
								Literal(m))))
					.AddStatements(
						ReturnStatement(
							MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								returnType,
								IdentifierName(m)))));
		}

		// Default: throw
		var valueName = isState ? "name" : "signal";
		switchArms.Add(
			SwitchSection()
				.AddLabels(
					DefaultSwitchLabel())
				.AddStatements(
					ThrowStatement(
						ObjectCreationExpression(
							ParseTypeName("global::System.ArgumentException"))
						.WithArgumentList(
							ArgumentList(
								SingletonSeparatedList(
									Argument(
										BinaryExpression(
											SyntaxKind.AddExpression,
											LiteralExpression(
												SyntaxKind.StringLiteralExpression,
												Literal($"Unknown {className}: ")),
											IdentifierName(valueName)))))))));

		// Build method body
		var bodyStatements = new List<StatementSyntax>();

		if (isState) {
			// var dot = stateKey.IndexOf('.');
			bodyStatements.Add(
				LocalDeclarationStatement(
					VariableDeclaration(
						IdentifierName("var"))
						.WithVariables(
							SingletonSeparatedList(
								VariableDeclarator(Identifier("dot"))
									.WithInitializer(
										EqualsValueClause(
											InvocationExpression(
												MemberAccessExpression(
													SyntaxKind.SimpleMemberAccessExpression,
													IdentifierName("stateKey"),
													IdentifierName("IndexOf")))
												.WithArgumentList(
													ArgumentList(
														SingletonSeparatedList(
															Argument(
																LiteralExpression(
																	SyntaxKind.StringLiteralExpression,
																	Literal("."))))))))))));

			// var name = dot >= 0 ? stateKey.Substring(dot + 1) : stateKey;
			bodyStatements.Add(
				LocalDeclarationStatement(
					VariableDeclaration(
						IdentifierName("var"))
						.WithVariables(
							SingletonSeparatedList(
								VariableDeclarator(Identifier("name"))
									.WithInitializer(
										EqualsValueClause(
											ConditionalExpression(
												BinaryExpression(
													SyntaxKind.GreaterThanOrEqualExpression,
													IdentifierName("dot"),
													LiteralExpression(
														SyntaxKind.NumericLiteralExpression,
														Literal(0))),
												InvocationExpression(
													MemberAccessExpression(
														SyntaxKind.SimpleMemberAccessExpression,
														IdentifierName("stateKey"),
														IdentifierName("Substring")))
													.WithArgumentList(
														ArgumentList(
															SingletonSeparatedList(
																Argument(
																	BinaryExpression(
																		SyntaxKind.AddExpression,
																		IdentifierName("dot"),
																		LiteralExpression(
																			SyntaxKind.NumericLiteralExpression,
																			Literal(1))))))),
												IdentifierName("stateKey"))))))));
		}

		// switch (name/signal) { ... }
		var switchExpr = isState ? (ExpressionSyntax)IdentifierName("name") : IdentifierName("signal");
		bodyStatements.Add(SwitchStatement(switchExpr).AddSections(switchArms.ToArray()));

		// Build method parameters
		var paramList = isState
			? ParameterList(
				SeparatedList<ParameterSyntax>(new[] {
					Parameter(Identifier("stateKey"))
						.WithType(PredefinedType(Token(SyntaxKind.StringKeyword))),
					Parameter(Identifier("groupId"))
						.WithType(PredefinedType(Token(SyntaxKind.StringKeyword)))
				}))
			: ParameterList(
				SingletonSeparatedList(
					Parameter(Identifier("signal"))
						.WithType(PredefinedType(Token(SyntaxKind.StringKeyword)))));

		return ClassDeclaration(className)
			.WithModifiers(TokenList(
				Token(SyntaxKind.SealedKeyword)))
			.AddBaseListTypes(
				SimpleBaseType(interfaceType))
			.AddMembers(
				MethodDeclaration(returnType, Identifier(methodName))
					.WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
					.WithParameterList(paramList)
					.WithBody(Block(bodyStatements)));
	}

	private static string GetShortName(string fullType) {
		var name = fullType;
		if (name.StartsWith("global::"))
			name = name.Substring(8);
		var lastDot = name.LastIndexOf('.');
		return lastDot >= 0 ? name.Substring(lastDot + 1) : name;
	}
}
