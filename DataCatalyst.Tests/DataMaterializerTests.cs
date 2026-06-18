namespace DataCatalyst.Tests;

using System;
using System.Collections.Generic;
using DataCatalyst.Core;
using FluentAssertions;
using Xunit;

public struct MaterializerTestComponent : IComponent {
	public string Name { get; set; }
	public int Value { get; set; }
}

public struct AnotherTestComponent : IComponent {
	public float Ratio { get; set; }
}

public class MockEntity {
	public List<object> Components { get; } = new();

	public void Add<T>(T component) where T : notnull {
		Components.Add(component);
	}
}

public class DataMaterializerTests {
	[Fact]
	public void Materializer_AppliesRegisteredComponents_ToMockTarget() {
		// Arrange
		var entry = new DataEntry("Goblin");
		entry.Set(new MaterializerTestComponent { Name = "Goblin", Value = 100 });
		entry.Set(new AnotherTestComponent { Ratio = 1.5f });

		var entity = new MockEntity();

		var materializer = new DataMaterializer<MockEntity>();
		materializer.Register<MaterializerTestComponent>((ent, comp) => ent.Add(comp));
		materializer.Register<AnotherTestComponent>((ent, comp) => ent.Add(comp));

		// Act
		materializer.Materialize(entry, entity);

		// Assert
		entity.Components.Count.Should().Be(2);

		var c1 = (MaterializerTestComponent)entity.Components[0];
		c1.Name.Should().Be("Goblin");
		c1.Value.Should().Be(100);

		var c2 = (AnotherTestComponent)entity.Components[1];
		c2.Ratio.Should().Be(1.5f);
	}

	[Fact]
	public void Materializer_SkipsMissingComponents_WithoutThrowing() {
		// Arrange
		var entry = new DataEntry("Goblin");
		entry.Set(new MaterializerTestComponent { Name = "Goblin", Value = 100 });
		// AnotherTestComponent is missing from the entry

		var entity = new MockEntity();

		var materializer = new DataMaterializer<MockEntity>();
		materializer.Register<MaterializerTestComponent>((ent, comp) => ent.Add(comp));
		materializer.Register<AnotherTestComponent>((ent, comp) => ent.Add(comp));

		// Act
		Action act = () => materializer.Materialize(entry, entity);

		// Assert
		act.Should().NotThrow();
		entity.Components.Count.Should().Be(1);
		
		var c1 = (MaterializerTestComponent)entity.Components[0];
		c1.Name.Should().Be("Goblin");
		c1.Value.Should().Be(100);
	}
}
