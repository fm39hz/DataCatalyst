namespace UniversalDataDriven.Abstractions;

/// <summary>
///     Hook for cross-cutting work that runs after <see cref="ITypeEmitter.Emit" />. Use cases:
///     <list type="bullet">
///         <item>Maintain a registry of every UDDSG target generated this compilation (e.g. for a meta-table).</item>
///         <item>Validate cross-target invariants (referenced foreign keys, duplicate row IDs across tables).</item>
///         <item>Emit auxiliary source (e.g. JSON-Schema files written via <c>spc.AddSource</c> in some other tool).</item>
///     </list>
///     <para>Post-processors must NOT mutate <paramref name="emittedSource" />; that string is already final.</para>
/// </summary>
public interface IUddsgPostProcessor {
	void After(string emittedSource, UddsgGenerationContext ctx);
}
