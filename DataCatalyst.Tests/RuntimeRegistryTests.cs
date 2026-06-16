using FluentAssertions;
using DataCatalyst.Abstractions;
using DataCatalyst.Runtime;
using Xunit;

namespace DataCatalyst.Tests;

public class DataContextRegistryTests {
    [Fact]
    public void Register_StoresInitializer() {
        var invoked = false;
        DataContextRegistry.Register(o => invoked = true);
        DataContextRegistry.InitializeAll();
        invoked.Should().BeTrue();
    }

    [Fact]
    public void InitializeAll_AppliesOverrides() {
        DataOverride? captured = null;
        DataContextRegistry.Register(o => captured = o?.FirstOrDefault());
        DataContextRegistry.InitializeAll([new DataOverride { Target = "Test", Fields = { ["X"] = 1 } }]);
        captured.Should().NotBeNull();
        captured!.Target.Should().Be("Test");
        captured.Fields["X"].Should().Be(1);
    }

    [Fact]
    public void Reset_ClearsAll() {
        var invoked = false;
        DataContextRegistry.Register(o => invoked = true);
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
    public static bool Constructed;
    public TestPlugin() => Constructed = true;
}
