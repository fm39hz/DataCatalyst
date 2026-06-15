namespace FM39hz.DataCatalyst.Test.Support;

using System.Collections.Immutable;
using System.Reflection;
using FM39hz.DataCatalyst.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public abstract class IntegrationTestBase {
	private static readonly PortableExecutableReference[] DefaultReferences = [
		MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(DataBackend).Assembly.Location),
		MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
		MetadataReference.CreateFromFile(typeof(ImmutableArray).Assembly.Location),
	];

	protected static CSharpCompilation CreateCompilation(string source) {
		var syntaxTree = CSharpSyntaxTree.ParseText(source,
			CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12));

		return CSharpCompilation.Create("TestAssembly",
			syntaxTrees: [syntaxTree],
			references: DefaultReferences,
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	protected static GeneratorDriver CreateDriver(params AdditionalText[] additionalTexts) => CSharpGeneratorDriver.Create(
			generators: [new UniversalDataGenerator().AsSourceGenerator()],
			additionalTexts: additionalTexts,
			parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12),
			optionsProvider: null);

	protected static (Compilation Output, ImmutableArray<Diagnostic> Diagnostics, string[] GeneratedSources) RunGenerator(
		string source, params AdditionalText[] additionalTexts) {

		var compilation = CreateCompilation(source);
		var syntaxTreeArray = compilation.SyntaxTrees.ToArray();
		var originalCount = syntaxTreeArray.Length;
		var driver = CreateDriver(additionalTexts);

		driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

		var generated = output.SyntaxTrees
			.Skip(originalCount)
			.Select(t => t.GetText().ToString())
			.ToArray();

		return (output, diagnostics, generated);
	}

	protected static string? FindSource(string[] sources, string contains) {
		foreach (var s in sources) {
			if (s.Contains(contains)) {
				return s;
			}
		}
		return null;
	}
}
