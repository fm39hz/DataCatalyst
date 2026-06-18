namespace DataCatalyst;

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
public sealed class PrimitiveDiscoveryGenerator : IIncrementalGenerator {
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

	private static readonly DiagnosticDescriptor CycleWarning = new(
		id: "DC003",
		title: "Circular plugin dependency",
		messageFormat: "Circular dependency detected involving plugin '{0}'",
		category: "DataCatalyst",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private const string DataComponentAttr = "DataCatalyst.Abstractions.DataComponentAttribute";
	private const string DataPluginAttr = "DataCatalyst.Abstractions.DataPluginAttribute";
	private const string DataPluginIface = "DataCatalyst.Abstractions.IDataPlugin";

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		var primitives = context.SyntaxProvider.ForAttributeWithMetadataName(
			DataComponentAttr,
			static (node, _) => node is TypeDeclarationSyntax,
			static (ctx, _) => {
				var t = (INamedTypeSymbol)ctx.TargetSymbol;
				var error = t.TypeKind != TypeKind.Structure
						? ctx.TargetNode.GetLocation()
						: null;
				var fullType = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				return new PrimitiveResult(fullType, fullType, error);
			}).Collect();

		var plugins = context.SyntaxProvider.ForAttributeWithMetadataName(
			DataPluginAttr,
			static (node, _) => node is ClassDeclarationSyntax,
			static (ctx, _) => {
				var t = (INamedTypeSymbol)ctx.TargetSymbol;
				if (!t.AllInterfaces.Any(i => i.ToDisplayString() == DataPluginIface)) {
					return default((string, string, string[])?);
				}

				var fullType = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var deps = GetDeps(t.GetAttributes());
				return ((string FullType, string Id, string[] Deps)?)(fullType, t.Name, deps);
			})
			.Where(static p => p is not null)
			.Select(static (p, _) => p!.Value)
			.Collect();

		context.RegisterSourceOutput(
			plugins.Combine(primitives),
			static (spc, payload) => {
				var (pl, pr) = payload;

				foreach (var p in pr) {
					if (p.Warning is { } w) {
						spc.ReportDiagnostic(Diagnostic.Create(StructRequiredError, w));
					}
				}

				Emit(spc, pl, pr);
			});
	}

	private readonly struct PrimitiveResult(string? fullType, string? discrim, Location? warning) {
		public readonly string? FullType = fullType;
		public readonly string? Discrim = discrim;
		public readonly Location? Warning = warning;
	}

	private static string[] GetDeps(ImmutableArray<AttributeData> attrs) {
		foreach (var a in attrs) {
			if (a.AttributeClass == null || a.AttributeClass.ToDisplayString() != DataPluginAttr) {
				continue;
			}

			foreach (var n in a.NamedArguments) {
				if (n.Key == "DependsOn" && n.Value.Values is { Length: > 0 } vs) {
					var list = new List<string>(vs.Length);
					foreach (var v in vs) {
						if (v.Value is INamedTypeSymbol dt) {
							list.Add(dt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
						}
					}
					return [.. list];
				}
			}
		}
		return [];
	}

	private static string Discriminator(string fullType) {
		var s = fullType.StartsWith("global::") ? fullType.Substring(8) : fullType;
		return s.Replace('+', '.');
	}

	private static void Emit(SourceProductionContext spc,
		ImmutableArray<(string FullType, string Id, string[] Deps)> allPlugins,
		ImmutableArray<PrimitiveResult> allPrims) {

		var prims = new List<(string FullType, string Discrim)>();
		var seenDiscrims = new HashSet<string>();
		var colliding = new HashSet<string>();

		foreach (var p in allPrims) {
			if (p.FullType == null) {
				continue;
			}

			var d = p.Discrim ?? Discriminator(p.FullType);
			if (!seenDiscrims.Add(d)) {
				colliding.Add(d);
			}
			prims.Add((p.FullType, d));
		}

		if (colliding.Count > 0) {
			foreach (var d in colliding) {
				var resolved = prims
					.Where(p => p.Discrim == d)
					.Select(p => Discriminator(p.FullType))
					.ToList();
				var msg = string.Join(", ", resolved);
				spc.ReportDiagnostic(Diagnostic.Create(CollisionWarning, Location.None, d, msg));
			}
			for (var i = 0; i < prims.Count; i++) {
				var (ft, d) = prims[i];
				if (colliding.Contains(d)) {
					prims[i] = (ft, Discriminator(ft));
				}
			}
		}

		if (allPlugins.Length == 0 && prims.Count == 0) {
			return;
		}

		// Build AST for Init() body via SyntaxFactory
		var initBody = new List<StatementSyntax>();

		if (allPlugins.Length > 0) {
			var sorted = TopoSort([.. allPlugins], spc);
			foreach (var (ft, _, _) in sorted) {
				initBody.Add(BuildRegisterCall("global::DataCatalyst.Core.PluginRegistry.Default", ft));
			}
		}

		if (prims.Count > 0) {
			foreach (var (ft, _) in prims) {
				initBody.Add(BuildRegisterCall("global::DataCatalyst.Core.PrimitiveRegistry.Default", ft));
			}
			initBody.Add(BuildRegisterIdsStatement(prims));
		}

		// Build AST for RegisterTo() body via SyntaxFactory
		var regBody = new List<StatementSyntax>();
		if (allPlugins.Length > 0) {
			var sorted = TopoSort([.. allPlugins], spc);
			foreach (var (ft, _, _) in sorted) {
				regBody.Add(BuildGenericCall("registry", "RegisterPlugin", ft));
			}
		}
		if (prims.Count > 0) {
			foreach (var (ft, _) in prims) {
				regBody.Add(BuildGenericCall("registry", "RegisterComponent", ft));
			}
		}

		// Assemble full CompilationUnitSyntax (entirely SyntaxFactory, no strings)
		var compilationUnit = CompilationUnit()
			.WithLeadingTrivia(Comment("// <auto-generated/>\n#nullable enable"))
			.AddMembers(
				NamespaceDeclaration(IdentifierName("DataCatalyst.Core"))
					.AddMembers(
						ClassDeclaration("PrimitiveRegistrations")
							.WithModifiers(TokenList(
								Token(SyntaxKind.PublicKeyword),
								Token(SyntaxKind.StaticKeyword),
								Token(SyntaxKind.PartialKeyword)))
							.AddMembers(
								// Init() method
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
								// RegisterTo() method
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
									.WithBody(Block(regBody))
							)
					)
			)
			.NormalizeWhitespace();

		spc.AddSource("PrimitiveRegistrations.g.cs",
			SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8));
	}

	// SyntaxFactory AST — Register call: target.Register<FullType>()
	private static StatementSyntax BuildRegisterCall(string target, string fullType) => ExpressionStatement(
			InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseExpression(target),
					GenericName("Register")
						.WithTypeArgumentList(
							TypeArgumentList(
								SingletonSeparatedList(ParseTypeName(fullType)))))));

	// SyntaxFactory AST — generic call: instance.Method<FullType>()
	private static StatementSyntax BuildGenericCall(string instance, string method, string fullType) => ExpressionStatement(
			InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParseExpression(instance),
					GenericName(method)
						.WithTypeArgumentList(
							TypeArgumentList(
								SingletonSeparatedList(ParseTypeName(fullType)))))));

	// SyntaxFactory AST — RegisterIds(new() { { "discrim", typeof(T) }, ... })
	private static StatementSyntax BuildRegisterIdsStatement(List<(string FullType, string Discrim)> prims) {
		var elems = new List<ExpressionSyntax>();
		foreach (var (ft, d) in prims) {
			elems.Add(
				InitializerExpression(
					SyntaxKind.ComplexElementInitializerExpression,
					SeparatedList<ExpressionSyntax>(
						[
							LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(d)),
							TypeOfExpression(ParseTypeName(ft))
						])));
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
								ImplicitObjectCreationExpression()
									.WithInitializer(
										InitializerExpression(
											SyntaxKind.CollectionInitializerExpression,
											SeparatedList(elems))))))));
	}

	private static List<(string FullType, string Id, string[] Deps)> TopoSort(
		List<(string FullType, string Id, string[] Deps)> plugins,
		SourceProductionContext spc) {

		var map = new Dictionary<string, (string FullType, string Id, string[] Deps)>();
		var indeg = new Dictionary<string, int>();
		var edges = new Dictionary<string, List<string>>();

		foreach (var p in plugins) {
			if (map.ContainsKey(p.FullType)) {
				continue;
			}

			map[p.FullType] = p;
			indeg[p.FullType] = 0;
			edges[p.FullType] = [];
		}

		foreach (var (FullType, Id, Deps) in plugins) {
			if (!map.ContainsKey(FullType)) {
				continue;
			}

			foreach (var d in Deps) {
				if (map.ContainsKey(d)) {
					edges[d].Add(FullType);
					indeg[FullType]++;
				}
			}
		}

		var ready = new Queue<string>();
		foreach (var kv in map) {
			if (indeg.TryGetValue(kv.Key, out var d) && d == 0) {
				ready.Enqueue(kv.Key);
			}
		}

		var result = new List<(string FullType, string Id, string[] Deps)>();
		while (ready.Count > 0) {
			var cur = ready.Dequeue();
			result.Add(map[cur]);
			if (edges.TryGetValue(cur, out var list)) {
				foreach (var c in list) {
					if (--indeg[c] == 0 && map.ContainsKey(c)) {
						ready.Enqueue(c);
					}
				}
			}
		}

		foreach (var kv in map) {
			if (!result.Any(r => r.FullType == kv.Key)) {
				spc.ReportDiagnostic(Diagnostic.Create(CycleWarning, Location.None, kv.Key));
			}
		}

		foreach (var p in plugins) {
			if (!result.Any(r => r.FullType == p.FullType)) {
				result.Add(p);
			}
		}

		return result;
	}
}
