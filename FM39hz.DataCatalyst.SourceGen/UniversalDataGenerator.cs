namespace FM39hz.DataCatalyst;

using FM39hz.DataCatalyst.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
		context.RegisterPostInitializationOutput(static ctx => {
			ctx.AddSource("CatalystDataAttribute.g.cs", """
				namespace FM39hz.DataCatalyst;

				[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
				public sealed class CatalystDataAttribute : System.Attribute {
					public string JsonPath { get; }
					public string EntryPoint { get; }
					public System.Type TemplateType { get; }
					public string KeyField { get; init; } = string.Empty;
					public int Backend { get; init; } = 0;
					public bool ModSupport { get; init; } = false;

					public CatalystDataAttribute(string jsonPath, string entryPoint = "", System.Type templateType = null) {
						JsonPath = jsonPath;
						EntryPoint = entryPoint;
						TemplateType = templateType;
					}
				}
				""");

			ctx.AddSource("DataCatalystRuntime.g.cs", """
				namespace FM39hz.DataCatalyst.Runtime;

				using System.Collections.Generic;

				public interface IDataRepository<TKey, TValue> {
					TValue Get(TKey key);
					bool TryGet(TKey key, out TValue value);
					IEnumerable<TValue> GetAll();
					int Count { get; }
				}

				public interface IDslReader<TValue> {
					string FileExtension { get; }
					bool TryRead(string text, out TValue value);
				}

				public static class DslReaderRegistry {
					private static readonly Dictionary<(string extension, System.Type type), object> _readers = new();
					private static readonly object _lock = new();

					public static void Register<TValue>(IDslReader<TValue> reader) {
						lock (_lock) {
							var key = (reader.FileExtension, typeof(TValue));
							_readers[key] = reader;
						}
					}

					public static IEnumerable<IDslReader<TValue>> GetReaders<TValue>() {
						List<IDslReader<TValue>> snap;
						lock (_lock) {
							snap = new List<IDslReader<TValue>>(_readers.Count);
							var target = typeof(TValue);
							foreach (var kvp in _readers) {
								if (kvp.Key.type == target) {
									snap.Add((IDslReader<TValue>)kvp.Value);
								}
							}
						}

						return snap;
					}
				}

				public static class DataBackendConst {
					public const int None = 0;
					public const int Json = 1;
					public const int Sqlite = 2;
					public const int All = 3;
				}

				public static class DataBackendSelector {
					private static volatile FM39hz.DataCatalyst.Abstractions.DataBackend _current = 0;
					private static volatile bool _initialized;
					private static readonly object _lock = new();

					public static void Initialize(string? backendOverride = null) {
						var value = backendOverride ?? global::System.Environment.GetEnvironmentVariable("DATACATALYST_BACKEND");
						var parsed = (value?.ToLowerInvariant()) switch {
							"sqlite" => FM39hz.DataCatalyst.Abstractions.DataBackend.Sqlite,
							"json" => FM39hz.DataCatalyst.Abstractions.DataBackend.Json,
							"all" => FM39hz.DataCatalyst.Abstractions.DataBackend.All,
							_ => FM39hz.DataCatalyst.Abstractions.DataBackend.None,
						};
						lock (_lock) {
							_current = parsed;
							_initialized = true;
						}
					}

					public static FM39hz.DataCatalyst.Abstractions.DataBackend Current {
						get {
							if (!_initialized) {
								lock (_lock) {
									if (!_initialized) {
										Initialize();
									}
								}
							}
							return _current;
						}
					}
				}
				""");
		});

		var targets = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				DcConstants.CATALYST_DATA_ATTRIBUTE_METADATA,
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
