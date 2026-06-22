namespace DataCatalyst;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Collections.Immutable;

/// <summary>Shared config helper — reads [DataCatalystConfig] assembly attribute once.</summary>
public static class ConfigHelper {
	private const string AttrName = "DataCatalyst.Abstractions.DataCatalystConfigAttribute";

	/// <summary>Returns a pipeline that extracts config from [DataCatalystConfig] attribute.</summary>
	public static IncrementalValueProvider<AssemblyConfig> GetConfig(
		IncrementalGeneratorInitializationContext context) {

		return context.SyntaxProvider.ForAttributeWithMetadataName(
			AttrName,
			static (node, _) => true,
			static (ctx, _) => {
				var ns = "DataCatalyst.Generated";
				var attrs = new List<string>();

				foreach (var a in ctx.Attributes) {
					if (a.AttributeClass?.ToDisplayString() != AttrName) continue;
					foreach (var kv in a.NamedArguments) {
						if (kv.Key == "Namespace" && kv.Value.Value is string s)
							ns = s;
						if (kv.Key == "Attributes" && kv.Value.Value is ITypeSymbol[] types) {
							foreach (var t in types)
								attrs.Add(t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
						}
					}
				}

				return new AssemblyConfig(ns, attrs);
			})
			.Collect()
			.Select(static (arr, _) => {
				foreach (var c in arr)
					if (c != null) return c;
				return new AssemblyConfig("DataCatalyst.Generated", new List<string>());
			});
	}
}

/// <summary>Resolved config from [DataCatalystConfig] attribute.</summary>
public sealed record AssemblyConfig(string Namespace, List<string> Attributes);
