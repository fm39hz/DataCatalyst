namespace DataCatalyst.Tests;

using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public class SourceGenTests {
	private static string RunGenerator(string sourceCode) {
		var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

		var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

		// Reference assemblies needed for compilation.
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

		// Assert no syntax or semantic errors in the test code setup
		var compileDiagnostics = compilation.GetDiagnostics();
		var errors = compileDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
		errors.Should()
			.BeEmpty(
				$"The input source code failed to compile: {string.Join(Environment.NewLine, errors.Select(e => e.GetMessage()))}");

		var generator = new PrimitiveDiscoveryGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

		var runResult = driver.GetRunResult();
		runResult.GeneratedTrees.Should().NotBeEmpty("The generator should have generated source code.");

		return runResult.GeneratedTrees[0].ToString();
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
		generated.Should().Contain("global::DataCatalyst.Core.PrimitiveRegistry.Register<global::MyGame.Health>();");
		generated.Should().Contain("public static void RegisterTo(global::DataCatalyst.Core.DataRegistry registry)");
		generated.Should().Contain("registry.RegisterComponent<global::MyGame.Health>();");
	}

	[Fact]
	public void Generator_RegistersPluginsTopologically() {
		var source = @"
			using System;
			using DataCatalyst.Abstractions;
			namespace MyGame {
				[DataPlugin(DependsOn = new[] { typeof(PluginB) })]
				public class PluginA : IDataPlugin { }

				[DataPlugin]
				public class PluginB : IDataPlugin { }
			}
		";

		var generated = RunGenerator(source);
		generated.Should().Contain("global::DataCatalyst.Core.PluginRegistry.Register<global::MyGame.PluginB>();");
		generated.Should().Contain("global::DataCatalyst.Core.PluginRegistry.Register<global::MyGame.PluginA>();");
		generated.Should().Contain("registry.RegisterPlugin<global::MyGame.PluginB>();");
		generated.Should().Contain("registry.RegisterPlugin<global::MyGame.PluginA>();");

		// Verify topological order in RegisterTo: PluginB must be registered before PluginA
		var indexA = generated.IndexOf("registry.RegisterPlugin<global::MyGame.PluginA>()");
		var indexB = generated.IndexOf("registry.RegisterPlugin<global::MyGame.PluginB>()");

		indexA.Should().BeGreaterThan(-1, "PluginA registration should be present in generated code.");
		indexB.Should().BeGreaterThan(-1, "PluginB registration should be present in generated code.");
		indexB.Should().BeLessThan(indexA, "PluginB (dependency) must be registered before PluginA (dependent).");
	}
}

// namespace DataCatalyst.Tests
