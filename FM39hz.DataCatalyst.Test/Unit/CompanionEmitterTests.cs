namespace FM39hz.DataCatalyst.Test.Unit;

using FluentAssertions;
using FM39hz.DataCatalyst.Core;
using FM39hz.DataCatalyst.Plugins.Emitters;
using Xunit;

public sealed class CompanionEmitterTests {

	// --- Registry Discovery (requires all plugin assemblies loaded) ---

	[Fact]
	public void CompanionEmitters_ShouldContainSqliteDataEmitter() => DcPluginRegistry.CompanionEmitters
			.Should().Contain(e => e is SqliteDataEmitter);

	[Fact]
	public void CompanionEmitters_ShouldContainJsonRuntimeDataEmitter() => DcPluginRegistry.CompanionEmitters
			.Should().Contain(e => e is JsonRuntimeDataEmitter);

	[Fact]
	public void CompanionEmitters_ShouldContainModOverlayDataEmitter() => DcPluginRegistry.CompanionEmitters
			.Should().Contain(e => e is ModOverlayDataEmitter);

	[Fact]
	public void CompanionEmitters_ShouldContainLuaBridgeEmitter() => DcPluginRegistry.CompanionEmitters
			.Should().Contain(e => e is LuaBridgeEmitter);

	[Fact]
	public void CompanionEmitters_Count_ShouldBeFour() => DcPluginRegistry.CompanionEmitters
			.Should().HaveCount(4);
}
