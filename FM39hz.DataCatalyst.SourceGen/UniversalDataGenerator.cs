namespace FM39hz.DataCatalyst;

using System.Collections.Generic;
using System.Collections.Immutable;
using FM39hz.DataCatalyst.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Note: `Where`/`Select` on IncrementalValuesProvider are extension methods in Microsoft.CodeAnalysis,
// not LINQ. No System.Linq using is needed.

/// <summary>
///     Universal Data-Driven Source Generator (DataCatalyst). Reads JSON files declared as <c>AdditionalFiles</c>
///     and emits a strongly-typed, reflection-free static registry into any partial type tagged with
///     <c>[CatalystData(...)]</c>. Materializes definitions at compile time
///     so game assemblies stay Native AOT / trimming friendly - consumers never parse JSON or reflect over rows at runtime.
///     <para>
///         All generation logic lives in plugins under <c>FM39hz.DataCatalyst.Plugins.*</c> and is wired
///         through the static <see cref="DcPluginRegistry" /> via <c>[ModuleInitializer]</c> when the analyzer loads.
///         That initializer runs in the compiler/Roslyn process only - not in shipped game binaries.
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
				public System.Type[]? RefTo { get; init; }

				public CatalystDataAttribute(string jsonPath, string entryPoint = "", System.Type templateType = null) {
						JsonPath = jsonPath;
						EntryPoint = entryPoint;
						TemplateType = templateType;
					}
				}

				[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
				public sealed class ModPluginAttribute : System.Attribute {
					public string Name { get; }
					public string[] Dependencies { get; }

					public ModPluginAttribute(string name, string[] dependencies = null) {
						Name = name;
						Dependencies = dependencies ?? [];
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

				public interface IDataViewAdapter<T> {
					void OnEntryAdded(string key, T entry);
					void OnEntryRemoved(string key);
					void OnEntryModified(string key, T oldEntry, T newEntry);
					void OnAllCleared();
				}

				public static class DataViewAdapterRegistry {
					private static readonly Dictionary<System.Type, object> _adapters = new();
					private static readonly object _lock = new();

					public static void Register<T>(IDataViewAdapter<T> adapter) {
						lock (_lock) {
							var t = typeof(T);
							if (_adapters.TryGetValue(t, out var existing)) {
								var list = (List<IDataViewAdapter<T>>)existing;
								list.Add(adapter);
							} else {
								_adapters[t] = new List<IDataViewAdapter<T>> { adapter };
							}
						}
					}

					public static IEnumerable<IDataViewAdapter<T>> GetAdapters<T>() {
						lock (_lock) {
							if (_adapters.TryGetValue(typeof(T), out var existing)) {
								var list = (List<IDataViewAdapter<T>>)existing;
								return list.ToArray();
							}
						}
						return [];
					}
				}

				public interface IModPlugin {
					string Name { get; }
					string[] Dependencies { get; }
					void OnLoad(IModGameContext context);
				}

				public interface IModGameContext {
					T? GetService<T>() where T : class;
					void RegisterService<T>(T service) where T : class;
				}

				public static class ServiceRegistry {
					private static readonly Dictionary<System.Type, object> _services = new();
					private static readonly object _lock = new();

					public static void Register<T>(T service) where T : class {
						lock (_lock) { _services[typeof(T)] = service; }
					}

					public static T? Get<T>() where T : class {
						lock (_lock) {
							return _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
						}
					}
				}

				public sealed class ModGameContext : IModGameContext {
					public T? GetService<T>() where T : class => ServiceRegistry.Get<T>();
					public void RegisterService<T>(T service) where T : class => ServiceRegistry.Register(service);
				}

				public static class PluginRegistry {
					private static readonly List<IModPlugin> _plugins = [];

					public static void Register(IModPlugin plugin) {
						_plugins.Add(plugin);
					}

					public static void LoadAll(IModGameContext context) {
						var sorted = TopoSort();
						foreach (var plugin in sorted) {
							plugin.OnLoad(context);
						}
					}

					private static List<IModPlugin> TopoSort() {
						var ordered = new List<IModPlugin>(_plugins.Count);
						var visited = new HashSet<string>();
						var visiting = new HashSet<string>();
						var map = new Dictionary<string, IModPlugin>();
						foreach (var p in _plugins) {
							map[p.Name] = p;
						}
						foreach (var p in _plugins) {
							Visit(p, map, visited, visiting, ordered);
						}
						return ordered;
					}

					private static void Visit(IModPlugin p, Dictionary<string, IModPlugin> map,
						HashSet<string> visited, HashSet<string> visiting, List<IModPlugin> ordered) {
						if (!visited.Add(p.Name)) return;
						visiting.Add(p.Name);
						foreach (var dep in p.Dependencies) {
							if (map.TryGetValue(dep, out var depPlugin)) {
								if (visiting.Contains(dep)) {
									continue;
								}
								Visit(depPlugin, map, visited, visiting, ordered);
							}
						}
						visiting.Remove(p.Name);
						ordered.Add(p);
					}
				}

				public readonly struct DataRef<TTarget, TTargetKind> where TTargetKind : struct {
					public TTargetKind Kind { get; }
					public DataRef(TTargetKind kind) => Kind = kind;
				}

				public static class CatalogRegistry {
					private static readonly List<System.Type> _catalogs = new();

					public static void Register<T>() {
						lock (_catalogs) {
							if (!_catalogs.Contains(typeof(T))) _catalogs.Add(typeof(T));
						}
					}

					public static System.Type[] GetAll() {
						lock (_catalogs) return _catalogs.ToArray();
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

				PipelineDriver.Reset();
				var sorted = TopoSortCatalogs(ts);
				foreach (var t in sorted) {
					PipelineDriver.Run(spc, additionalTexts, t);
				}
			});

		var modPlugins = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				DcConstants.MOD_PLUGIN_ATTRIBUTE_METADATA,
				static (node, _) => node is ClassDeclarationSyntax,
				static (ctx, _) => {
					if (ctx.TargetSymbol is not INamedTypeSymbol type) {
						return null;
					}

					AttributeData? attr = null;
					foreach (var a in ctx.Attributes) {
						attr = a;
						break;
					}

					if (attr is null) {
						return null;
					}

					var name = string.Empty;
					var dependencies = System.Array.Empty<string>();

					if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string n) {
						name = n;
					}

					if (attr.ConstructorArguments.Length >= 2 && attr.ConstructorArguments[1].Values is var deps) {
						var list = new System.Collections.Generic.List<string>();
						foreach (var d in deps) {
							var s = d.Value?.ToString();
							if (!string.IsNullOrEmpty(s)) {
								list.Add(s!);
							}
						}
						dependencies = [.. list];
					}

					foreach (var na in attr.NamedArguments) {
						switch (na.Key) {
							case "Name" when na.Value.Value is string ns:
								name = ns;
								break;
							case "Dependencies" when na.Value.Values is { Length: > 0 } vs:
								var list = new System.Collections.Generic.List<string>();
								foreach (var v in vs) {
									var s = v.Value?.ToString();
									if (!string.IsNullOrEmpty(s)) {
										list.Add(s!);
									}
								}
								dependencies = [.. list];
								break;
							default:
								break;
						}
					}

					if (string.IsNullOrEmpty(name)) {
						name = type.Name;
					}

					var fullType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
					return ((string Name, string FullType, string[] Dependencies)?)(Name: name, FullType: fullType, Dependencies: dependencies);
				})
			.Where(static p => p.HasValue)
			.Select(static (p, _) => p!.Value)
			.Collect();

		context.RegisterSourceOutput(modPlugins, static (spc, plugins) => {
			if (plugins.IsDefaultOrEmpty) {
				return;
			}

			var sb = new System.Text.StringBuilder();
			var codegenHeader = "// <auto-generated/>\n#nullable enable\n\nnamespace FM39hz.DataCatalyst.Runtime;\n\npublic static partial class ModPluginRegistrations {\n";
			var codegenFooter = "\n}";
			sb.Append(codegenHeader);

			var seen = new System.Collections.Generic.HashSet<string>();
			foreach (var (name, fullType, deps) in plugins) {
				if (!seen.Add(name)) {
					continue;
				}

				sb.Append("\t[System.Runtime.CompilerServices.ModuleInitializer]\n");
				sb.Append("\tinternal static void Register_").Append(System.Text.RegularExpressions.Regex.Replace(name, "[^a-zA-Z0-9]", "_")).Append("() =>\n");
				sb.Append("\t\tglobal::FM39hz.DataCatalyst.Runtime.PluginRegistry.Register(new ").Append(fullType).AppendLine("());\n");
			}

			sb.Append(codegenFooter);
			spc.AddSource("ModPluginRegistrations.g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(sb.ToString(), System.Text.Encoding.UTF8));
		});
	}

	private static ImmutableArray<TargetInfo> TopoSortCatalogs(ImmutableArray<TargetInfo> targets) {
		var map = new Dictionary<string, TargetInfo>();
		var indegree = new Dictionary<string, int>();
		var edges = new Dictionary<string, List<string>>();

		foreach (var t in targets) {
			map[t.SimpleName] = t;
			indegree[t.SimpleName] = 0;
			edges[t.SimpleName] = new List<string>();
		}

		foreach (var t in targets) {
			if (t.RefToTargets.Length == 0) continue;
			foreach (var rt in t.RefToTargets) {
				var dot = rt.LastIndexOf('.');
				var simple = dot >= 0 ? rt.Substring(dot + 1) : rt;
				if (!map.ContainsKey(simple)) continue;
				edges[simple].Add(t.SimpleName);
				indegree[t.SimpleName]++;
			}
		}

		var ready = new List<TargetInfo>();
		foreach (var t in targets) {
			if (indegree[t.SimpleName] == 0) ready.Add(t);
		}

		ready.Sort(static (a, b) => string.CompareOrdinal(a.SimpleName, b.SimpleName));
		var ordered = new List<TargetInfo>(targets.Length);

		while (ready.Count > 0) {
			var cur = ready[0];
			ready.RemoveAt(0);
			ordered.Add(cur);
			foreach (var child in edges[cur.SimpleName]) {
				indegree[child]--;
				if (indegree[child] == 0) {
					ready.Add(map[child]);
				}
			}
			ready.Sort(static (a, b) => string.CompareOrdinal(a.SimpleName, b.SimpleName));
		}

		if (ordered.Count < targets.Length) {
			ordered.Clear();
			foreach (var t in targets) ordered.Add(t);
		}

		var b = ImmutableArray.CreateBuilder<TargetInfo>(ordered.Count);
		foreach (var t in ordered) b.Add(t);
		return b.MoveToImmutable();
	}
}
