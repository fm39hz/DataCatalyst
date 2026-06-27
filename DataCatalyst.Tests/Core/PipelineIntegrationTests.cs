namespace DataCatalyst.Tests.Core;

using DataCatalyst.Loader;
using DataCatalyst.Loaders;
using DataCatalyst.Registry;
using Xunit;

public class PipelineIntegrationTests {
	private static string FindTestDataDir() {
		var dir = AppContext.BaseDirectory;
		while (dir != null && !Directory.Exists(Path.Combine(dir, "Data"))) {
			dir = Directory.GetParent(dir)?.FullName;
		}

		return dir ?? AppContext.BaseDirectory;
	}

	[Fact]
	public void RegistrySet_CreateAndFreeze_DoesNotThrow() {
		var rs = new RegistrySet();
		rs.Freeze();
		Assert.True(rs.Frozen);
	}

	[Fact]
	public void JsonDataLoader_CanLoad_WithoutRegistry() {
		var loader = new JsonDataLoader();
		var result = loader.Load(/*lang=json,strict*/ "{\"Test\": {\"key\": \"value\"}}", "test");
		Assert.NotNull(result);
		Assert.Single(result.Beings);
	}

	[Fact]
	public void JsonDataLoader_CanLoad_WithRegistry() {
		var registry = new BeingRegistry();
		registry.Register<DummyBeing>(typeof(DummyConcept));
		var loader = new JsonDataLoader(registry);
		var result = loader.Load(/*lang=json,strict*/ "{\"$Concept\": {\"TestAspect\": 42}}", "test");
		Assert.NotNull(result);
		Assert.Single(result.Beings);
		Assert.NotNull(result.Beings[0]);
	}

	[Fact]
	public void LoadResult_AddMethods_Work() {
		var r = new LoadResult();
		r.AddDiagnostic("duplicate key detected");
		Assert.Single(r.Diagnostics);
		Assert.Contains("duplicate", r.Diagnostics[0], StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Pipeline_CreatesWithRegistrySet() {
		var rs = new RegistrySet();
		var pipeline = new Pipeline.Pipeline(rs);
		Assert.NotNull(pipeline);
		Assert.Same(rs, pipeline.Registries);
	}
}
