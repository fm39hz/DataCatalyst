namespace DataCatalyst.Tests;

using System.Reflection;
using System.Text;
using DataCatalyst.Abstractions;
using DataCatalyst.Core;
using DataCatalyst.Plugins.GameConcept;
using DataCatalyst.Plugins.StateEngine;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

public class SourceGenTests {
	private static readonly string[] DefaultRefs = GetDefaultRefs();

	private static string[] GetDefaultRefs() {
		var list = new HashSet<string> {
			typeof(object).Assembly.Location,
			typeof(Enumerable).Assembly.Location,
			typeof(int).Assembly.Location,
			typeof(Attribute).Assembly.Location,
			typeof(DataComponentAttribute).Assembly.Location,
			typeof(DataRegistry).Assembly.Location,
			typeof(PluginRegistry).Assembly.Location,
			typeof(DataConceptAttribute).Assembly.Location,
			typeof(ConceptRegistry).Assembly.Location,
			typeof(StateMachineGenerator).Assembly.Location,
		};
		// Add netstandard reference — needed by Roslyn compilation
		try { list.Add(Assembly.Load("netstandard").Location); } catch { }
		try { list.Add(Assembly.Load("System.Runtime").Location); } catch { }
		try { list.Add(Assembly.Load("System.Collections").Location); } catch { }
		try { list.Add(Assembly.Load("System.Linq").Location); } catch { }
		return [.. list];
	}

	private static string RunGenerator<T>(string source, T generator, IEnumerable<AdditionalText>? additionalTexts = null)
		where T : IIncrementalGenerator {

		var syntaxTree = CSharpSyntaxTree.ParseText(source);
		var refs = DefaultRefs.Select(r => MetadataReference.CreateFromFile(r)).ToArray();

		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[syntaxTree],
			refs,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		var errors = compilation.GetDiagnostics()
			.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
		errors.Should().BeEmpty($"Input failed to compile: {string.Join("; ", errors.Select(e => e.GetMessage()))}");

		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			new ISourceGenerator[] { generator.AsSourceGenerator() },
			additionalTexts: additionalTexts?.ToArray());

		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
		var runResult = driver.GetRunResult();
		runResult.GeneratedTrees.Should().NotBeEmpty("Generator should produce output");
		return string.Join("\n//===\n", runResult.GeneratedTrees.Select(t => t.ToString()));
	}

	[Fact]
	public void Generator_RegistersComponents() {
		var generated = RunGenerator(
			"using DataCatalyst.Abstractions;\n[DataComponent] public struct Health { public float Max; }",
			new ComponentGenerator());

		generated.Should().Contain("Register<global::Health>");
	}

	[Fact]
	public void PluginGenerator_RegistersPlugins() {
		var generated = RunGenerator(
			"using DataCatalyst.Abstractions;\n" +
			"public class PluginA : IPlugin { public bool IsEnabled => true; public void OnLoad() { } }",
			new PluginGenerator());

		generated.Should().Contain("Register<global::PluginA>");
		generated.Should().Contain("LoadAll");
	}

	[Fact]
	public void ConceptGenerator_RegistersConcepts() {
		var generated = RunGenerator(
			"using DataCatalyst.Plugins.GameConcept;\n" +
			"[DataConcept(\"Item\")] public readonly partial struct Item;",
			new ConceptGenerator());

		generated.Should().Contain("Register<global::Item>(\"Item\")");
	}

	[Fact]
	public void StateMachineGenerator_GeneratesMappers() {
		var generated = RunGenerator(
			"using DataCatalyst.Plugins.GameConcept;\n" +
			"[DataConcept(\"AIState\")] public enum AIState { Idle, Attack }",
			new StateMachineGenerator());

		generated.Should().Contain("AIStateStateMapper");
		generated.Should().Contain("AIStateSensorMapper");
		generated.Should().Contain("MapState");
		generated.Should().Contain("MapSensor");
	}

	[Fact]
	public void ConceptsFromDataGenerator_GeneratesNestedConceptType() {
		var json = "{ \"Weapon\": {} }";
		var additionalTexts = new[] { new AdditionalTextStub("concepts.json", json) };

		var generated = RunGenerator(
			"namespace Dummy;",
			new ConceptsFromDataGenerator(),
			additionalTexts);

		generated.Should().Contain("public static partial class Concept");
		generated.Should().Contain("public readonly partial struct Weapon");
		generated.Should().Contain("[DataConcept(\"Weapon\")]");
	}
}

internal sealed class AdditionalTextStub(string path, string text) : AdditionalText {
	private readonly SourceText _source = SourceText.From(text, Encoding.UTF8);
	public override string Path => path;
	public override SourceText? GetText(CancellationToken cancellationToken = default) => _source;
}
