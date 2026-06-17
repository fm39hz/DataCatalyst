namespace DataCatalyst.Tests; 
using DataCatalyst.Abstractions;
using DataCatalyst.Core;
using FluentAssertions;
using Xunit;

public class AbstractionsTests : IDisposable {
	public AbstractionsTests() {
		ServiceRegistry.Clear();
		DataViewAdapterRegistry.Clear();
	}

	public void Dispose() {
		ServiceRegistry.Clear();
		DataViewAdapterRegistry.Clear();
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void DataKey_Constructor_SetsId() {
		var key = new DataKey<TestStruct>("my_id");
		key.Id.Should().Be("my_id");
		key.HasValue.Should().BeTrue();
	}

	[Fact]
	public void DataKey_Default_HasNoValue() {
		var key = new DataKey<TestStruct>();
		key.Id.Should().BeNull();
		key.HasValue.Should().BeFalse();
		key.ToString().Should().Be("");
	}

	[Fact]
	public void DataKey_ToString_ReturnsId() {
		var key = new DataKey<TestStruct>("player");
		key.ToString().Should().Be("player");
	}

	[Fact]
	public void DataKey_Equality_Works() {
		var key1 = new DataKey<TestStruct>("player");
		var key2 = new DataKey<TestStruct>("player");
		var key3 = new DataKey<TestStruct>("enemy");

		key1.Should().Be(key2);
		key1.Should().NotBe(key3);
	}

	[Fact]
	public void ServiceRegistry_RegisterAndResolve() {
		var service = new TestService { Name = "Auth" };
		ServiceRegistry.Register(service);

		var resolved = ServiceRegistry.Get<TestService>();
		resolved.Should().NotBeNull();
		resolved!.Name.Should().Be("Auth");
	}

	[Fact]
	public void ServiceRegistry_ResolveUnregistered_ReturnsNull() {
		var resolved = ServiceRegistry.Get<UnregisteredService>();
		resolved.Should().BeNull();
	}

	[Fact]
	public void DataViewAdapterRegistry_RegisterAndGet() {
		var adapter = new TestViewAdapter();
		DataViewAdapterRegistry.Register(adapter);

		var adapters = DataViewAdapterRegistry.GetAdapters<TestStruct>();
		adapters.Should().ContainSingle().And.Contain(adapter);
	}

	[Fact]
	public void DataOverride_Constructor_SetsProperties() {
		var dataOverride = new DataOverride {
			Target = "item_sword",
			RawJson = /*lang=json,strict*/ "{ \"Damage\": 50 }"
		};

		dataOverride.Target.Should().Be("item_sword");
		dataOverride.RawJson.Should().Be(/*lang=json,strict*/ "{ \"Damage\": 50 }");
	}
}

public class TestService {
	public string Name { get; set; } = "";
}

public class UnregisteredService { }

public class TestViewAdapter : IDataViewAdapter<TestStruct> {
	public void OnEntryAdded(string key, TestStruct entry) { }
	public void OnEntryRemoved(string key) { }
	public void OnEntryModified(string key, TestStruct oldEntry, TestStruct newEntry) { }
	public void OnAllCleared() { }
}

 // namespace DataCatalyst.Tests
