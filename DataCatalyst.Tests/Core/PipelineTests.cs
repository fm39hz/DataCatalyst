namespace DataCatalyst.Tests.Core;

using DataCatalyst.Loader;
using DataCatalyst.Loaders;
using DataCatalyst.Pipeline;
using DataCatalyst.Registry;
using FluentAssertions;
using Xunit;

public class PipelineTests {
    [Fact]
    public void TopoSort_ReturnsEmpty_ForEmptyInput() {
        var result = Pipeline.TopoSort([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void TopoSort_ReturnsSingle_ForSingleSource() {
        var s = new DataSource("A", new JsonDataLoader(), "/path/a");
        var result = Pipeline.TopoSort([s]);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("A");
    }

    [Fact]
    public void TopoSort_PreservesOrder_WhenNoDependencies() {
        var a = new DataSource("A", new JsonDataLoader(), "/path/a");
        var b = new DataSource("B", new JsonDataLoader(), "/path/b");
        var result = Pipeline.TopoSort([a, b]);
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("A");
        result[1].Name.Should().Be("B");
    }

    [Fact]
    public void TopoSort_OrdersByDependency() {
        var a = new DataSource("A", new JsonDataLoader(), "/path/a");
        var b = new DataSource("B", new JsonDataLoader(), "/path/b") { DependsOn = ["A"] };
        var result = Pipeline.TopoSort([b, a]);
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("A");
        result[1].Name.Should().Be("B");
    }

    [Fact]
    public void TopoSort_Throws_OnCycle() {
        var a = new DataSource("A", new JsonDataLoader(), "/path/a") { DependsOn = ["B"] };
        var b = new DataSource("B", new JsonDataLoader(), "/path/b") { DependsOn = ["C"] };
        var c = new DataSource("C", new JsonDataLoader(), "/path/c") { DependsOn = ["A"] };
        Action act = () => Pipeline.TopoSort([a, b, c]);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TopoSort_Throws_OnMutualDependency() {
        var a = new DataSource("A", new JsonDataLoader(), "/path/a") { DependsOn = ["B"] };
        var b = new DataSource("B", new JsonDataLoader(), "/path/b") { DependsOn = ["A"] };
        Action act = () => Pipeline.TopoSort([a, b]);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_AddsDefaultStages() {
        var pipeline = new Pipeline(new RegistrySet());
        pipeline._stages.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithNoDefaults_AddsNoStages() {
        var pipeline = new Pipeline(new RegistrySet(), noDefaults: true);
        pipeline._stages.Should().BeEmpty();
    }
}
