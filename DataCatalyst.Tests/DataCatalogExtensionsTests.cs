namespace DataCatalyst.Tests;

using DataCatalyst.Abstractions;
using DataCatalyst.Core;
using FluentAssertions;
using Xunit;

public struct TestComponent : IComponent {
	public string Name { get; set; }
	public int Value { get; set; }
}

public class DataCatalogExtensionsRelaxedTests {
	[Fact]
	public void Bind_WithDataKey_Succeeds() {
		// Arrange
		var entry1 = new DataEntry("H2O", new() {
			[typeof(TestComponent)] = new TestComponent { Name = "H2O", Value = 18 }
		});

		var entry2 = new DataEntry("O2", new() {
			[typeof(TestComponent)] = new TestComponent { Name = "O2", Value = 32 }
		});

		var graph = DataGraphBuilder.Build([entry1, entry2]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		// Act
		var bound = catalog.Bind<DataKey<TestComponent>, TestComponent>(c => new DataKey<TestComponent>(c.Name));

		// Assert
		bound.Count.Should().Be(2);
		bound[new DataKey<TestComponent>("H2O")].Value.Should().Be(18);
		bound[new DataKey<TestComponent>("O2")].Value.Should().Be(32);
	}

	[Fact]
	public void Bind_WithCustomKey_Succeeds() {
		// Arrange
		var entry1 = new DataEntry("H2O", new() {
			[typeof(TestComponent)] = new TestComponent { Name = "H2O", Value = 18 }
		});

		var graph = DataGraphBuilder.Build([entry1]);
		var catalog = DataCatalogBuilder.Resolve(graph);

		// Act
		var bound = catalog.Bind<DataKey<TestComponent>, TestComponent>(c => new DataKey<TestComponent>(c.Name));

		// Assert
		bound.Count.Should().Be(1);
		bound[new DataKey<TestComponent>("H2O")].Value.Should().Be(18);
	}
}
