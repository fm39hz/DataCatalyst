namespace FM39hz.DataCatalyst.Abstractions;

using System.Collections.Generic;

/// <summary>
///     Builds a <see cref="SchemaInfo" /> describing every column the generated type will expose.
///     Two built-in providers ship with DataCatalyst:
///     <list type="bullet">
///         <item><c>InferenceSchemaProvider</c>: walks the rows and infers types via <see cref="IPrimitiveTypeRule" />.</item>
///         <item><c>TemplateSchemaProvider</c>: copies the schema from a user-declared template type.</item>
///     </list>
///     Providers must be mutually exclusive — at most one returns <c>true</c> from
///     <see cref="Applies(DcGenerationContext)" /> for any given context.
/// </summary>
public interface ISchemaProvider {
	public string Name { get; }

	public bool Applies(DcGenerationContext ctx);

	public SchemaInfo? Build(IReadOnlyList<RowData> rows, DcGenerationContext ctx);
}
