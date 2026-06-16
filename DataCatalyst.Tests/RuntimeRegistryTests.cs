using FluentAssertions;
using DataCatalyst.Abstractions;
using DataCatalyst.Runtime;
using Xunit;

namespace DataCatalyst.Tests;

public class DataContextRegistryTests {
    struct TestData { public int X; }

    [Fact]
    public void Register_StoresHandler() {
        int? captured = null;
        DataContextRegistry.Register<TestData>(json => {
            if (json is null) captured = 0;
            else captured = json.Value.GetProperty("x").GetInt32();
        });
        DataContextRegistry.InitializeAll();
        captured.Should().Be(0);
    }

    [Fact]
    public void InitializeAll_AppliesOverride() {
        int? captured = null;
        DataContextRegistry.Register<TestData>(json => {
            if (json is not null) captured = json.Value.GetProperty("x").GetInt32();
        });
        DataContextRegistry.InitializeAll([
            new DataOverride { Target = "TestData", RawJson = """{"x":42}""" }
        ]);
        captured.Should().Be(42);
    }

    [Fact]
    public void Reset_ClearsAll() {
        var invoked = false;
        DataContextRegistry.Register<TestData>(json => invoked = true);
        DataContextRegistry.Reset();
        DataContextRegistry.InitializeAll();
        invoked.Should().BeFalse();
    }
}

public class DataDslRegistryTests {
    [Fact]
    public void RegisterAndCheck() {
        DataDslRegistry.Register<string>();
        DataDslRegistry.IsRegistered<string>().Should().BeTrue();
    }

    [Fact]
    public void Unregistered_ReturnsFalse() {
        DataDslRegistry.IsRegistered<DataDslRegistryTests>().Should().BeFalse();
    }

    [Fact]
    public void GetAll_ReturnsRegistered() {
        DataDslRegistry.Register<string>();
        DataDslRegistry.GetAll().Should().Contain(typeof(string));
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

public class TestPlugin : IDataPlugin {
    public static bool Constructed { get; set; }
    public TestPlugin() => Constructed = true;
}
