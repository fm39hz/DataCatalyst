using DataCatalyst.Abstractions;
using DataCatalyst.Core;
using FluentAssertions;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DataCatalyst.Tests;

public class DataEntryTests {
	[Fact]
	public void Constructor_SetsKey() {
		var entry = new DataEntry("hero");
		entry.Key.Should().Be("hero");
	}

	[Fact]
	public void SetAndGet_Roundtrips() {
		var entry = new DataEntry("test", new() {
			[typeof(TestStruct)] = new TestStruct { X = 42 }
		});
		entry.Get<TestStruct>().X.Should().Be(42);
	}

	[Fact]
	public void TryGet_Missing_ReturnsFalse() {
		var entry = new DataEntry("empty");
		entry.TryGet<TestStruct>(out _).Should().BeFalse();
	}

	[Fact]
	public void TryGet_Present_ReturnsTrue() {
		var entry = new DataEntry("x", new() {
			[typeof(TestStruct)] = new TestStruct { X = 1 }
		});
		entry.TryGet<TestStruct>(out var v).Should().BeTrue();
		v.X.Should().Be(1);
	}

	[Fact]
	public void Has_ChecksType() {
		var entry = new DataEntry("x", new() {
			[typeof(TestStruct)] = new TestStruct()
		});
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
		var graph = DataGraphBuilder.Build([entry]);
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
		var e = new DataEntry("x", new() {
			[typeof(TestStruct)] = new TestStruct { X = 1 }
		});
		var graph = DataGraphBuilder.Build([e]);
		var catalog = DataCatalogBuilder.Resolve(graph);
		catalog.Get<TestStruct>("x").X.Should().Be(1);
	}

	[Fact]
	public void Inheritance_MergesParentComponents() {
		var parent = new DataEntry("parent", new() {
			[typeof(TestStruct)] = new TestStruct { X = 10 }
		});

		var child = new DataEntry("child", new() {
			[typeof(OtherStruct)] = new OtherStruct { Y = 20 }
		}, inherits: ["parent"]);

		var graph = DataGraphBuilder.Build([parent, child]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		catalog.Entries["child"].Get<TestStruct>().X.Should().Be(10);
		catalog.Entries["child"].Get<OtherStruct>().Y.Should().Be(20);
	}

	[Fact]
	public void ChildOverridesParent() {
		var parent = new DataEntry("parent", new() {
			[typeof(TestStruct)] = new TestStruct { X = 10 }
		});

		var child = new DataEntry("child", new() {
			[typeof(TestStruct)] = new TestStruct { X = 99 }
		}, inherits: ["parent"]);

		var graph = DataGraphBuilder.Build([parent, child]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		catalog.Get<TestStruct>("child").X.Should().Be(99);
	}

	[Fact]
	public void DeepChain_FlattensAll() {
		var a = new DataEntry("a", new() {
			[typeof(TestStruct)] = new TestStruct { X = 1 }
		});
		var b = new DataEntry("b", new() {
			[typeof(OtherStruct)] = new OtherStruct { Y = 2 }
		}, inherits: ["a"]);
		var c = new DataEntry("c", inherits: ["b"]);

		var graph = DataGraphBuilder.Build([a, b, c]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		catalog.Get<TestStruct>("c").X.Should().Be(1);
		catalog.Get<OtherStruct>("c").Y.Should().Be(2);
	}

	[Fact]
	public void Cycle_Throws() {
		var a = new DataEntry("a", inherits: ["b"]);
		var b = new DataEntry("b", inherits: ["a"]);
		var graph = DataGraphBuilder.Build([a, b]);

		Action act = () => DataCatalogBuilder.Resolve(graph);
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void MissingParent_Ignores() {
		var child = new DataEntry("child", new() {
			[typeof(TestStruct)] = new TestStruct { X = 5 }
		}, inherits: ["missing"]);
		var graph = DataGraphBuilder.Build([child]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		catalog.Get<TestStruct>("child").X.Should().Be(5);
	}

	[Fact]
	public void TryGet_FromCatalog() {
		var e = new DataEntry("x", new() {
			[typeof(TestStruct)] = new TestStruct { X = 7 }
		});
		var catalog = DataCatalogBuilder.Resolve(DataGraphBuilder.Build([e]));
		catalog.TryGet<TestStruct>("x", out var v).Should().BeTrue();
		v.X.Should().Be(7);
		catalog.TryGet<OtherStruct>("x", out _).Should().BeFalse();
	}

	[Fact]
	public void ContainsKey_Works() {
		var e = new DataEntry("x");
		var catalog = DataCatalogBuilder.Resolve(DataGraphBuilder.Build([e]));
		catalog.ContainsKey("x").Should().BeTrue();
		catalog.ContainsKey("y").Should().BeFalse();
	}
}

public class PrimitiveRegistryTests : IDisposable {
	private readonly PrimitiveRegistry _registry = new();

	public void Dispose() {
		_registry.Clear();
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void RegisterAndCheck() {
		_registry.Register<TestStruct>();
		_registry.IsRegistered(typeof(TestStruct)).Should().BeTrue();
	}

	[Fact]
	public void Unregistered_ReturnsFalse() => new PrimitiveRegistry().IsRegistered(typeof(string)).Should().BeFalse();

	[Fact]
	public void GetAll_ReturnsRegistered() {
		_registry.Register<TestStruct>();
		_registry.GetAll().Should().Contain(typeof(TestStruct));
	}

	[Fact]
	public void Clear_Works() {
		_registry.Register<TestStruct>();
		_registry.Clear();
		_registry.GetAll().Should().BeEmpty();
	}
}

public class PluginRegistryTests : IDisposable {
	private readonly PluginRegistry _registry = new();

	public void Dispose() {
		_registry.Clear();
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void Register_CreatesInstance() {
		_registry.Register<TestPlugin>();
		_registry.Plugins.Should().Contain(p => p is TestPlugin);
	}

	[Fact]
	public void Register_InstantiatesPlugin() {
		TestPlugin.Constructed = false;
		_registry.Register<TestPlugin>();
		TestPlugin.Constructed.Should().BeTrue();
	}

}

public struct TestStruct : IComponent {
	public int X { get; set; }
}

public struct OtherStruct : IComponent {
	public int Y { get; set; }
}

/// <summary>
/// Marker interface for data components — consumer-defined.
/// Not referenced by DataCatalyst.Core; used by the game project
/// to denote structs that can be stored in a DataEntry.
/// </summary>
public interface IComponent {
}

public class TestPlugin : IDataPlugin {
	public static bool Constructed { get; set; }

	public TestPlugin() {
		Constructed = true;
	}
}

public class DataCatalogExtensionsTests {
	public enum BindTestKind { A, B }

	public struct BindTestStruct {
		public BindTestKind Kind { get; set; }
		public int Value { get; set; }
	}

	[Fact]
	public void Bind_MapsEnumCorrectly() {
		var e1 = new DataEntry("A", new() {
			[typeof(BindTestStruct)] = new BindTestStruct { Kind = BindTestKind.A, Value = 10 }
		});
		var e2 = new DataEntry("B", new() {
			[typeof(BindTestStruct)] = new BindTestStruct { Kind = BindTestKind.B, Value = 20 }
		});

		var graph = DataGraphBuilder.Build([e1, e2]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		var result = catalog.Bind<BindTestKind, BindTestStruct>(s => s.Kind);

		result.Should().HaveCount(2);
		result[BindTestKind.A].Value.Should().Be(10);
		result[BindTestKind.B].Value.Should().Be(20);
	}
}

public class DataRegistryTests {
	[Fact]
	public void RegisterComponent_StoresType() {
		var registry = new DataRegistry();
		registry.RegisterComponent<TestStruct>();
		registry.GetComponents().Should().Contain(typeof(TestStruct));
	}

	[Fact]
	public void RegisterPlugin_StoresPluginInstance() {
		var registry = new DataRegistry();
		registry.RegisterPlugin<TestPlugin>();
		registry.GetPlugins().Should().Contain(p => p is TestPlugin);
	}
}
