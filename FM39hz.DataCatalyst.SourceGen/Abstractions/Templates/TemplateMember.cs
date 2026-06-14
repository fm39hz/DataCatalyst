namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Immutable;

public enum TemplateMemberKind { Property, Field }

/// <summary>
///     A single property or field on the optional <c>TemplateType</c> declared in <c>[CatalystData]</c>.
///     Schema providers consume <see cref="ITemplateMetadata" /> to map JSON columns onto user-defined C# types.
/// </summary>
public sealed class TemplateMember {
	public string Name { get; }
	public string TypeFullyQualified { get; }
	public TemplateMemberKind Kind { get; }

	public TemplateMember(string name, string typeFullyQualified, TemplateMemberKind kind) {
		Name = name;
		TypeFullyQualified = typeFullyQualified;
		Kind = kind;
	}
}

/// <summary>Read-only view of a DataCatalyst target's optional template type.</summary>
public interface ITemplateMetadata {
	string FullyQualifiedName { get; }
	ImmutableArray<TemplateMember> Members { get; }
}
