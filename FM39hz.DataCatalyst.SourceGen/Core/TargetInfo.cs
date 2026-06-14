namespace FM39hz.DataCatalyst.Core;

using System.Collections.Immutable;
using System.Linq;
using FM39hz.DataCatalyst.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
///     Captures everything the pipeline driver needs to know about a single target type tagged with
///     <c>[CatalystData]</c>. This is the symbol-extraction step's output and the only place that
///     touches Roslyn semantic-model types: every downstream plugin works against the equivalent fields
///     wrapped in <see cref="DcGenerationContext" />.
/// </summary>
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
		Location = location;
	}

	public static TargetInfo? Extract(GeneratorAttributeSyntaxContext ctx) {
		if (ctx.TargetSymbol is not INamedTypeSymbol type) {
			return null;
		}

		var attr = ctx.Attributes.FirstOrDefault();
		if (attr is null) {
			return null;
		}

		var jsonPath = string.Empty;
		var entryPoint = string.Empty;
		var keyField = string.Empty;
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
				default:
					break;
			}
		}

		var loc = ctx.TargetNode.GetLocation();
		var isPartial = false;
		foreach (var syntaxRef in type.DeclaringSyntaxReferences) {
			if (syntaxRef.GetSyntax() is TypeDeclarationSyntax tds && tds.Modifiers.Any(m => m.ValueText == "partial")) {
				isPartial = true;
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
