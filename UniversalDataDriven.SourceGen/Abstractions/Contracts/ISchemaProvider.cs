namespace UniversalDataDriven.Abstractions;

using System.Collections.Generic;

/// <summary>
///     Builds a <see cref="SchemaInfo" /> describing every column the generated type will expose.
///     Two built-in providers ship with UDDSG:
///     <list type="bullet">
///         <item><c>InferenceSchemaProvider</c>: walks the rows and infers types via <see cref="IPrimitiveTypeRule" />.</item>
///         <item><c>TemplateSchemaProvider</c>: copies the schema from a user-declared template type.</item>
///     </list>
///     Providers must be mutually exclusive — at most one returns <c>true</c> from
///     <see cref="Applies(UddsgGenerationContext)" /> for any given context.
/// </summary>
public interface ISchemaProvider {
	string Name { get; }

	bool Applies(UddsgGenerationContext ctx);

	SchemaInfo? Build(IReadOnlyList<RowData> rows, UddsgGenerationContext ctx);
}
