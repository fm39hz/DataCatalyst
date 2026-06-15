namespace FM39hz.DataCatalyst.Core;

using System.Collections.Immutable;
using FM39hz.DataCatalyst.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed class TargetInfo {
	public string FullyQualifiedName { get; }
	public string? ContainingNamespace { get; }
	public string SimpleName { get; }
	public TypeKind TypeKind { get; }
	public bool IsRecord { get; }
	public bool IsPartial { get; }
	public ITemplateMetadata? Template { get; }
	public string JsonPath { get; }
	public string EntryPoint { get; }
	public string KeyField { get; }
	public DataBackend Backend { get; }
	public bool ModSupport { get; }
	public ImmutableArray<string> RefToTargets { get; }
	public int LoadMode { get; }
	public Location Location { get; }

	private TargetInfo(
		string fullyQualifiedName,
		string? containingNamespace,
		string simpleName,
		TypeKind typeKind,
		bool isRecord,
		bool isPartial,
		ITemplateMetadata? template,
		string jsonPath,
		string entryPoint,
		string keyField,
		DataBackend backend,
		bool modSupport,
		ImmutableArray<string> refToTargets,
		int loadMode,
		Location location) {
		FullyQualifiedName = fullyQualifiedName;
		ContainingNamespace = containingNamespace;
		SimpleName = simpleName;
		TypeKind = typeKind;
		IsRecord = isRecord;
		IsPartial = isPartial;
		Template = template;
		JsonPath = jsonPath;
		EntryPoint = entryPoint;
		KeyField = keyField;
		Backend = backend;
		ModSupport = modSupport;
		RefToTargets = refToTargets;
		Location = location;
	}

	public static TargetInfo? Extract(GeneratorAttributeSyntaxContext ctx) {
		if (ctx.TargetSymbol is not INamedTypeSymbol type) {
			return null;
		}

		AttributeData? attr = null;
		foreach (var a in ctx.Attributes) { attr = a; break; }
		if (attr is null) {
			return null;
		}

		var jsonPath = string.Empty;
		var entryPoint = string.Empty;
		var keyField = string.Empty;
		var backend = DataBackend.None;
		var modSupport = false;
		var refToBuilder = ImmutableArray.CreateBuilder<string>();
		var loadMode = 0;
		ITypeSymbol? template = null;

		if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string jp) {
			jsonPath = jp;
		}

		if (attr.ConstructorArguments.Length >= 2 && attr.ConstructorArguments[1].Value is string ep) {
			entryPoint = ep;
		}

		if (attr.ConstructorArguments.Length >= 3 && attr.ConstructorArguments[2].Value is INamedTypeSymbol tt) {
			template = tt;
		}

		foreach (var na in attr.NamedArguments) {
			switch (na.Key) {
				case "JsonPath" when na.Value.Value is string s:
					jsonPath = s;
					break;
				case "EntryPoint" when na.Value.Value is string s2:
					entryPoint = s2;
					break;
				case "TemplateType" when na.Value.Value is INamedTypeSymbol nt:
					template = nt;
					break;
				case "KeyField" when na.Value.Value is string kf:
					keyField = kf;
					break;
				case "Backend" when na.Value.Value is int b:
					backend = (DataBackend)b;
					break;
				case "ModSupport" when na.Value.Value is bool ms:
					modSupport = ms;
					break;
				case "LoadMode" when na.Value.Value is int lm:
					loadMode = lm;
					break;
				case "RefTo" when na.Value.Values is { Length: > 0 } refTypes:
					foreach (var r in refTypes) {
						if (r.Value is INamedTypeSymbol rt) {
							refToBuilder.Add(rt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
						}
					}
					break;
				default:
					break;
			}
		}

		if (modSupport && !backend.HasFlag(DataBackend.Json)) {
			backend |= DataBackend.Json;
		}

		var loc = ctx.TargetNode.GetLocation();
		var isPartial = false;
		foreach (var syntaxRef in type.DeclaringSyntaxReferences) {
			if (syntaxRef.GetSyntax() is TypeDeclarationSyntax tds) {
				foreach (var m in tds.Modifiers) {
					if (m.ValueText == "partial") { isPartial = true; break; }
				}
				break;
			}
		}

		ITemplateMetadata? templateMeta = null;
		if (template is INamedTypeSymbol templateNamed) {
			templateMeta = new TemplateMetadata(
				templateNamed.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				CollectTemplateMembers(templateNamed));
		}

		return new TargetInfo(
			fullyQualifiedName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			containingNamespace: type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
			simpleName: type.Name,
			typeKind: type.TypeKind,
			isRecord: type.IsRecord,
			isPartial: isPartial,
			template: templateMeta,
			jsonPath: jsonPath,
			entryPoint: entryPoint,
			keyField: keyField,
			backend: backend,
			modSupport: modSupport,
			refToTargets: refToBuilder.ToImmutable(),
			loadMode: loadMode,
			location: loc);
	}

	private static ImmutableArray<TemplateMember> CollectTemplateMembers(INamedTypeSymbol template) {
		var b = ImmutableArray.CreateBuilder<TemplateMember>();
		foreach (var m in template.GetMembers()) {
			switch (m) {
				case IPropertySymbol { IsStatic: false, IsIndexer: false } p when p.DeclaredAccessibility == Accessibility.Public:
					b.Add(new TemplateMember(p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), TemplateMemberKind.Property));
					break;
				case IFieldSymbol { IsStatic: false, IsConst: false } f when f.DeclaredAccessibility == Accessibility.Public:
					b.Add(new TemplateMember(f.Name, f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), TemplateMemberKind.Field));
					break;
				default:
					break;
			}
		}
		return b.ToImmutable();
	}

	private sealed class TemplateMetadata(string fqn, ImmutableArray<TemplateMember> members) : ITemplateMetadata {
		public string FullyQualifiedName { get; } = fqn;
		public ImmutableArray<TemplateMember> Members { get; } = members;
	}
}
