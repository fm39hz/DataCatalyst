namespace FM39hz.DataCatalyst;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using FM39hz.DataCatalyst.Core;

// Note: `Where`/`Select` on IncrementalValuesProvider are extension methods in Microsoft.CodeAnalysis,
// not LINQ. No System.Linq using is needed.

/// <summary>
///     Universal Data-Driven Source Generator (DataCatalyst). Reads JSON files declared as <c>AdditionalFiles</c>
///     and emits a strongly-typed, reflection-free static registry into any partial type tagged with
///     <c>[CatalystData(...)]</c>. Materializes definitions at compile time
///     so game assemblies stay Native AOT / trimming friendly — consumers never parse JSON or reflect over rows at runtime.
///     <para>
///         All generation logic lives in plugins under <c>FM39hz.DataCatalyst.Plugins.*</c> and is wired
///         through the static <see cref="DcPluginRegistry" /> via <c>[ModuleInitializer]</c> when the analyzer loads.
///         That initializer runs in the compiler/Roslyn process only — not in shipped game binaries.
///         This generator is just the Roslyn pipeline shim: it harvests target types and hands them to
///         <see cref="PipelineDriver.Run" />.
///     </para>
/// </summary>
[Generator]
public sealed class UniversalDataGenerator : IIncrementalGenerator {
	public void Initialize(IncrementalGeneratorInitializationContext context) {
		var targets = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				DcConstants.CatalystDataAttributeMetadata,
				static (node, _) => node is TypeDeclarationSyntax,
				static (ctx, _) => TargetInfo.Extract(ctx))
			.Where(static t => t is not null)
			.Select(static (t, _) => t!);

		var combined = context.AdditionalTextsProvider.Collect()
			.Combine(targets.Collect())
			.Combine(context.CompilationProvider);

		context.RegisterSourceOutput(
			combined,
			static (spc, payload) => {
				var ((additionalTexts, ts), _) = payload;
				if (ts.IsDefaultOrEmpty) {
					return;
				}

				foreach (var t in ts) {
					PipelineDriver.Run(spc, additionalTexts, t);
				}
			});
	}
}
