namespace Catalyst.Tests.Core;

using Catalyst.Registry;
using Catalyst.Storage;
using Xunit;

public class RegistryTests {
	[Fact]
	public void BeingRegistry_RegisterAndAll() {
		var r = new BeingRegistry();
		Assert.False(r.Frozen);
		Assert.Empty(r.All);

		r.Register<DummyBeing>(typeof(DummyConcept));
		Assert.Single(r.All);
		Assert.Equal(typeof(DummyBeing), r.All[0].BeingType);
	}

	[Fact]
	public void BeingRegistry_FreezePreventsMutation() {
		var r = new BeingRegistry();
		r.Freeze();
		Assert.True(r.Frozen);
		Assert.Throws<InvalidOperationException>(() => r.Register<DummyBeing>(typeof(DummyConcept)));
	}

	[Fact]
	public void BeingRegistry_CreatePoolAfterFreeze() {
		var r = new BeingRegistry();
		r.RegisterPool(typeof(DummyConcept), () => new DummyPool());
		r.Freeze();

		var pool = r.CreatePool(typeof(DummyConcept));
		Assert.NotNull(pool);

		var missing = r.CreatePool(typeof(string));
		Assert.Null(missing);
	}

	[Fact]
	public void RequiresRegistry_RegisterAndGet() {
		var r = new RequiresRegistry();
		r.Register("TestConcept", ["AspectA", "AspectB"], ["AspectC"]);
		Assert.Equal(["AspectA", "AspectB"], r.GetRequired("TestConcept"));
		Assert.Equal(["AspectC"], r.GetSuggested("TestConcept"));
		Assert.True(r.HasConcept("TestConcept"));
	}

	[Fact]
	public void RequiresRegistry_UnknownConceptReturnsEmpty() {
		var r = new RequiresRegistry();
		Assert.Empty(r.GetRequired("Unknown"));
		Assert.Empty(r.GetSuggested("Unknown"));
		Assert.False(r.HasConcept("Unknown"));
	}

	[Fact]
	public void AspectFieldRegistry_RegisterAndGet() {
		var r = new AspectFieldRegistry();
		var fields = new Dictionary<string, Type> { { "Health", typeof(int) } };
		r.Register("TestAspect", fields);
		var got = r.GetFields("TestAspect");
		Assert.NotNull(got);
		Assert.Equal(typeof(int), got["Health"]);
	}

	[Fact]
	public void AspectFieldRegistry_GetFieldsReturnsCopy() {
		var r = new AspectFieldRegistry();
		r.Register("Test", new Dictionary<string, Type> { { "X", typeof(int) } });
		var got = r.GetFields("Test")!;
		got["X"] = typeof(string);
		Assert.Equal(typeof(int), r.GetFields("Test")!["X"]);
	}

	[Fact]
	public void AspectTypeRegistry_RegisterAndResolve() {
		var r = new AspectTypeRegistry();
		r.Register(typeof(DummyAspect));
		Assert.True(r.HasType("DummyAspect"));
		Assert.True(r.TryGetType("DummyAspect", out var t));
		Assert.Equal(typeof(DummyAspect), t);
	}

	[Fact]
	public void AspectTypeRegistry_Deserialize() {
		var r = new AspectTypeRegistry();
		r.Register(typeof(DummyAspect));
		r.RegisterDeserializer(typeof(DummyAspect), o => new DummyAspect { Value = (int)(o ?? 0) });
		var result = r.Deserialize(typeof(DummyAspect), 42);
		Assert.NotNull(result);
		Assert.Equal(42, ((DummyAspect)result).Value);
	}

	[Fact]
	public void AspectTypeRegistry_DeserializeNullRawReturnsNull() {
		var r = new AspectTypeRegistry();
		r.Register(typeof(DummyAspect));
		r.RegisterDeserializer(typeof(DummyAspect), o => new DummyAspect());
		Assert.Null(r.Deserialize(typeof(DummyAspect), null));
	}

	[Fact]
	public void RegistrySet_FreezeAll() {
		var rs = new RegistrySet();
		Assert.False(rs.Frozen);
		rs.Freeze();
		Assert.True(rs.Frozen);
	}
}

public struct DummyConcept : IConcept { }
public struct DummyBeing : IBeing { }
public struct DummyAspect { public int Value; }

public class DummyPool : ITypedStoragePool {
	public int Count => 0;
	public void Resize(int size) { }
	public T Get<T>(int index) where T : struct => throw new ArgumentOutOfRangeException(nameof(index));
	public void Set<T>(int index, T value) where T : struct { }
}
