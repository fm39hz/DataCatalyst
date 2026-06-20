namespace DataCatalyst.Tests;

using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class SourceGenTests {
	private static string RunGenerator(string sourceCode) {
		var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

		var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

		var references = new List<MetadataReference> {
			MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Private.CoreLib.dll")),
			MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
			MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "netstandard.dll")),
			MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.ModuleInitializerAttribute).Assembly
				.Location),
			MetadataReference.CreateFromFile(typeof(Abstractions.DataComponentAttribute).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Core.DataRegistry).Assembly.Location)
		};

		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[syntaxTree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		var compileDiagnostics = compilation.GetDiagnostics();
		var errors = compileDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
		errors.Should()
			.BeEmpty(
				$"The input source code failed to compile: {string.Join(Environment.NewLine, errors.Select(e => e.GetMessage()))}");

		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			new ComponentGenerator(),
			new PluginGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

		var runResult = driver.GetRunResult();
		runResult.GeneratedTrees.Should().NotBeEmpty("The generator should have generated source code.");

		return string.Join("\n//=== Next file ===\n",
			runResult.GeneratedTrees.Select(t => t.ToString()));
	}

	[Fact]
	public void Generator_RegistersComponents() {
		var source = @"
			using DataCatalyst.Abstractions;
			namespace MyGame {
				[DataComponent]
				public struct Health {
					public float Max;
				}
			}
		";

		var generated = RunGenerator(source);
		generated.Should().Contain("global::DataCatalyst.Core.PrimitiveRegistry.Default.Register<global::MyGame.Health>();");
		generated.Should().Contain("public static void RegisterTo(global::DataCatalyst.Core.DataRegistry registry)");
		generated.Should().Contain("registry.RegisterComponent<global::MyGame.Health>();");
	}

	[Fact]
	public void Generator_RegistersPluginsTopologically() {
		var source = @"
			using System;
			using DataCatalyst.Abstractions;
			namespace MyGame {
				public class PluginA : IPlugin { public bool IsEnabled => true; public void OnLoad() { } }

				public class PluginB : IPlugin { public bool IsEnabled => true; public void OnLoad() { } }
			}
		";

		var generated = RunGenerator(source);
		generated.Should().Contain("global::DataCatalyst.Core.PluginRegistry.Default.Register<global::MyGame.PluginA>();");
		generated.Should().Contain("global::DataCatalyst.Core.PluginRegistry.Default.Register<global::MyGame.PluginB>();");
		generated.Should().Contain("registry.RegisterPlugin<global::MyGame.PluginA>();");
		generated.Should().Contain("registry.RegisterPlugin<global::MyGame.PluginB>();");
		generated.Should().Contain("LoadAll");
	}
}
