namespace Catalyst.Tests.Core;

using Catalyst.Schema;
using FluentAssertions;
using Xunit;

public class SchemaRegistryTests {
	[Fact]
	public void DefineAspect_AssignsId() {
		var reg = new SchemaRegistry();
		var id = reg.DefineAspect("Health", new() { { "Current", typeof(int) } });
		id.Should().Be(0);
		reg.GetAspectId("Health").Should().Be(0);
	}

	[Fact]
	public void DefineAspect_ReturnsSameId_ForDuplicateName() {
		var reg = new SchemaRegistry();
		var id1 = reg.DefineAspect("Health", new() { { "Current", typeof(int) } });
		var id2 = reg.DefineAspect("Health", new() { { "Max", typeof(int) } });
		id2.Should().Be(id1);
		reg.Aspects.Should().HaveCount(1);
	}

	[Fact]
	public void DefineConcept_AssignsId() {
		var reg = new SchemaRegistry();
		var id = reg.DefineConcept("Player", []);
		id.Should().Be(0);
		reg.GetConceptId("Player").Should().Be(0);
	}

	[Fact]
	public void DefineConcept_ReturnsSameId_ForDuplicateName() {
		var reg = new SchemaRegistry();
		var id1 = reg.DefineConcept("Player", []);
		var id2 = reg.DefineConcept("Player", []);
		id2.Should().Be(id1);
	}

	[Fact]
	public void DefineConcept_MapsAspects() {
		var reg = new SchemaRegistry();
		reg.DefineAspect("Health", new() { { "Current", typeof(int) } });
		reg.DefineAspect("Mana", new() { { "Current", typeof(int) } });
		var id = reg.DefineConcept("Player", ["Health", "Mana"]);
		reg.ConceptAspects.Should().ContainKey(id);
		reg.ConceptAspects[id].Should().HaveCount(2);
	}

	[Fact]
	public void GetAspectId_ReturnsNull_ForUnknown() {
		var reg = new SchemaRegistry();
		reg.GetAspectId("Unknown").Should().BeNull();
	}

	[Fact]
	public void GetConceptId_ReturnsNull_ForUnknown() {
		var reg = new SchemaRegistry();
		reg.GetConceptId("Unknown").Should().BeNull();
	}

	[Fact]
	public void TryGetAspectName_ReturnsTrue_ForKnown() {
		var reg = new SchemaRegistry();
		var id = reg.DefineAspect("Health", new() { { "Current", typeof(int) } });
		reg.TryGetAspectName(id, out var name).Should().BeTrue();
		name.Should().Be("Health");
	}

	[Fact]
	public void TryGetConceptName_ReturnsTrue_ForKnown() {
		var reg = new SchemaRegistry();
		var id = reg.DefineConcept("Player", []);
		reg.TryGetConceptName(id, out var name).Should().BeTrue();
		name.Should().Be("Player");
	}

	[Fact]
	public void HasAspect_ReturnsTrue_ForKnown() {
		var reg = new SchemaRegistry();
		var id = reg.DefineAspect("Health", new() { { "Current", typeof(int) } });
		reg.HasAspect(id).Should().BeTrue();
	}

	[Fact]
	public void HasConcept_ReturnsTrue_ForKnown() {
		var reg = new SchemaRegistry();
		var id = reg.DefineConcept("Player", []);
		reg.HasConcept(id).Should().BeTrue();
	}

	[Fact]
	public void MergeFrom_MergesNewAspects() {
		var dest = new SchemaRegistry();
		var src = new SchemaRegistry();
		src.DefineAspect("Health", new() { { "Current", typeof(int) } });
		dest.MergeFrom(src);
		dest.GetAspectId("Health").Should().NotBeNull();
	}

	[Fact]
	public void MergeFrom_MergesNewConcepts() {
		var dest = new SchemaRegistry();
		var src = new SchemaRegistry();
		src.DefineAspect("Health", new() { { "Current", typeof(int) } });
		src.DefineConcept("Player", ["Health"]);
		dest.MergeFrom(src);
		dest.GetConceptId("Player").Should().NotBeNull();
	}

	[Fact]
	public void MergeFrom_SkipsDuplicateAspects() {
		var dest = new SchemaRegistry();
		dest.DefineAspect("Health", new() { { "Current", typeof(int) } });
		var src = new SchemaRegistry();
		src.DefineAspect("Health", new() { { "Max", typeof(int) } });
		dest.MergeFrom(src);
		dest.Aspects.Should().HaveCount(1);
	}

	[Fact]
	public void MergeFrom_SkipsDuplicateConcepts() {
		var dest = new SchemaRegistry();
		dest.DefineConcept("Player", []);
		var src = new SchemaRegistry();
		src.DefineConcept("Player", []);
		dest.MergeFrom(src);
		dest.GetConceptId("Player").Should().Be(0);
	}
}
