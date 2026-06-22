namespace DataCatalyst;

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

/// <summary>Shared config helper — reads [DataCatalystConfig] assembly attributes.</summary>
public static class ConfigHelper {
	private const string AttrName = "DataCatalyst.Abstractions.DataCatalystConfigAttribute";

	public static IncrementalValueProvider<ImmutableArray<SourceConfig>> GetConfigs(
		IncrementalGeneratorInitializationContext context) {

		return context.SyntaxProvider.ForAttributeWithMetadataName(
			AttrName,
			static (node, _) => true,
			static (ctx, _) => {
				var path = "";
				var ns = "DataCatalyst.Generated";
				var attrs = new List<string>();

				foreach (var a in ctx.Attributes) {
					if (a.AttributeClass?.ToDisplayString() != AttrName) continue;

					// Constructor arg = sourcePath
					if (a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string s)
						path = s;

					foreach (var kv in a.NamedArguments) {
						if (kv.Key == "Namespace" && kv.Value.Value is string nsVal)
							ns = nsVal;
						if (kv.Key == "Attributes" && kv.Value.Value is IEnumerable<ITypeSymbol> types) {
							foreach (var t in types)
								attrs.Add(t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
						}
					}
				}

				return new SourceConfig(path, ns, attrs);
			})
			.Collect();
	}
}

public sealed record SourceConfig(string SourcePath, string Namespace, List<string> Attributes);
