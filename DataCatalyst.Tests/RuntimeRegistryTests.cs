using FluentAssertions;
using DataCatalyst.Abstractions;
using DataCatalyst.Core;
using Xunit;

namespace DataCatalyst.Tests;

public class DataEntryTests {
    [Fact]
    public void Constructor_SetsKey() {
        var entry = new DataEntry("hero");
        entry.Key.Should().Be("hero");
    }

    [Fact]
    public void SetAndGet_Roundtrips() {
        var entry = new DataEntry("test");
        entry.Set(new TestStruct { X = 42 });
        entry.Get<TestStruct>().X.Should().Be(42);
    }

    [Fact]
    public void TryGet_Missing_ReturnsFalse() {
        var entry = new DataEntry("empty");
        entry.TryGet<TestStruct>(out _).Should().BeFalse();
    }

    [Fact]
    public void TryGet_Present_ReturnsTrue() {
        var entry = new DataEntry("x");
        entry.Set(new TestStruct { X = 1 });
        entry.TryGet<TestStruct>(out var v).Should().BeTrue();
        v.X.Should().Be(1);
    }

    [Fact]
    public void Has_ChecksType() {
        var entry = new DataEntry("x");
        entry.Has<TestStruct>().Should().BeFalse();
        entry.Set(new TestStruct());
        entry.Has<TestStruct>().Should().BeTrue();
    }

    [Fact]
    public void Constructor_AcceptsComponents() {
        var comps = new Dictionary<Type, object> { [typeof(TestStruct)] = new TestStruct { X = 7 } };
        var entry = new DataEntry("c", comps);
        entry.Get<TestStruct>().X.Should().Be(7);
    }
}

public class DataGraphBuilderTests {
    [Fact]
    public void Build_FromEmpty_ReturnsEmpty() {
        var graph = DataGraphBuilder.Build([]);
        graph.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Build_SingleEntry() {
        var entry = new DataEntry("a");
        var graph = DataGraphBuilder.Build(new[] { entry });
        graph.Entries.Should().ContainKey("a");
    }

    [Fact]
    public void Build_MultipleEntries() {
        var entries = new[] { new DataEntry("a"), new DataEntry("b") };
        var graph = DataGraphBuilder.Build(entries);
        graph.Entries.Should().HaveCount(2);
    }
}

public class DataCatalogBuilderTests {
    [Fact]
    public void Resolve_SingleEntry() {
        var e = new DataEntry("x");
        e.Set(new TestStruct { X = 1 });
        var graph = DataGraphBuilder.Build(new[] { e });
        var catalog = DataCatalogBuilder.Resolve(graph);
        catalog.Get<TestStruct>("x").X.Should().Be(1);
    }

    [Fact]
    public void Inheritance_MergesParentComponents() {
        var parent = new DataEntry("parent");
        parent.Set(new TestStruct { X = 10 });

        var child = new DataEntry("child", inherits: new() { "parent" });
        child.Set(new OtherStruct { Y = 20 });

        var graph = DataGraphBuilder.Build(new[] { parent, child });
        var catalog = DataCatalogBuilder.Resolve(graph);

        catalog.Entries["child"].Get<TestStruct>().X.Should().Be(10);
        catalog.Entries["child"].Get<OtherStruct>().Y.Should().Be(20);
    }

    [Fact]
    public void ChildOverridesParent() {
        var parent = new DataEntry("parent");
        parent.Set(new TestStruct { X = 10 });

        var child = new DataEntry("child", inherits: new() { "parent" });
        child.Set(new TestStruct { X = 99 });

        var graph = DataGraphBuilder.Build(new[] { parent, child });
        var catalog = DataCatalogBuilder.Resolve(graph);

        catalog.Get<TestStruct>("child").X.Should().Be(99);
    }

    [Fact]
    public void DeepChain_FlattensAll() {
        var a = new DataEntry("a"); a.Set(new TestStruct { X = 1 });
        var b = new DataEntry("b", inherits: new() { "a" }); b.Set(new OtherStruct { Y = 2 });
        var c = new DataEntry("c", inherits: new() { "b" });

        var graph = DataGraphBuilder.Build(new[] { a, b, c });
        var catalog = DataCatalogBuilder.Resolve(graph);

        catalog.Get<TestStruct>("c").X.Should().Be(1);
        catalog.Get<OtherStruct>("c").Y.Should().Be(2);
    }

    [Fact]
    public void Cycle_Throws() {
        var a = new DataEntry("a", inherits: new() { "b" });
        var b = new DataEntry("b", inherits: new() { "a" });
        var graph = DataGraphBuilder.Build(new[] { a, b });

        Action act = () => DataCatalogBuilder.Resolve(graph);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MissingParent_Ignores() {
        var child = new DataEntry("child", inherits: new() { "missing" });
        child.Set(new TestStruct { X = 5 });
        var graph = DataGraphBuilder.Build(new[] { child });
        var catalog = DataCatalogBuilder.Resolve(graph);

        catalog.Get<TestStruct>("child").X.Should().Be(5);
    }

    [Fact]
    public void TryGet_FromCatalog() {
        var e = new DataEntry("x"); e.Set(new TestStruct { X = 7 });
        var catalog = DataCatalogBuilder.Resolve(DataGraphBuilder.Build(new[] { e }));
        catalog.TryGet<TestStruct>("x", out var v).Should().BeTrue();
        v.X.Should().Be(7);
        catalog.TryGet<OtherStruct>("x", out _).Should().BeFalse();
    }

    [Fact]
    public void ContainsKey_Works() {
        var e = new DataEntry("x");
        var catalog = DataCatalogBuilder.Resolve(DataGraphBuilder.Build(new[] { e }));
        catalog.ContainsKey("x").Should().BeTrue();
        catalog.ContainsKey("y").Should().BeFalse();
    }
}

public class PrimitiveRegistryTests {
    [Fact]
    public void RegisterAndCheck() {
        PrimitiveRegistry.Register<TestStruct>();
        PrimitiveRegistry.IsRegistered(typeof(TestStruct)).Should().BeTrue();
    }

    [Fact]
    public void Unregistered_ReturnsFalse() {
        PrimitiveRegistry.IsRegistered(typeof(string)).Should().BeFalse();
    }

    [Fact]
    public void GetAll_ReturnsRegistered() {
        PrimitiveRegistry.Clear();
        PrimitiveRegistry.Register<TestStruct>();
        PrimitiveRegistry.GetAll().Should().Contain(typeof(TestStruct));
    }

    [Fact]
    public void Clear_Works() {
        PrimitiveRegistry.Register<TestStruct>();
        PrimitiveRegistry.Clear();
        PrimitiveRegistry.GetAll().Should().BeEmpty();
    }
}

public class PluginRegistryTests {
    [Fact]
    public void Register_CreatesInstance() {
        PluginRegistry.Register<TestPlugin>();
        PluginRegistry.Plugins.Should().Contain(p => p is TestPlugin);
    }

    [Fact]
    public void Register_TriggersStaticCtor() {
        TestPlugin.Constructed = false;
        PluginRegistry.Register<TestPlugin>();
        TestPlugin.Constructed.Should().BeTrue();
    }
}

public struct TestStruct : IComponent { public int X; }
public struct OtherStruct : IComponent { public int Y; }
public interface IComponent { }

public class TestPlugin : IDataPlugin {
    public static bool Constructed { get; set; }
    public TestPlugin() => Constructed = true;
}
