namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Immutable;

public enum TemplateMemberKind { Property, Field }

/// <summary>
///     A single property or field on the optional <c>TemplateType</c> declared in <c>[CatalystData]</c>.
///     Schema providers consume <see cref="ITemplateMetadata" /> to map JSON columns onto user-defined C# types.
/// </summary>
public sealed class TemplateMember(string name, string typeFullyQualified, TemplateMemberKind kind) {
	public string Name { get; } = name;
	public string TypeFullyQualified { get; } = typeFullyQualified;
	public TemplateMemberKind Kind { get; } = kind;
}

/// <summary>Read-only view of a DataCatalyst target's optional template type.</summary>
public interface ITemplateMetadata {
	public string FullyQualifiedName { get; }
	public ImmutableArray<TemplateMember> Members { get; }
}
