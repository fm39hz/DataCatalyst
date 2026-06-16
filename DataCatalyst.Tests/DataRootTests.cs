using System.Collections.Immutable;
using FluentAssertions;
using DataCatalyst.DataRoot;
using Xunit;

namespace DataCatalyst.Tests;

public class InheritanceGraphTests {
    [Fact]
    public void SingleNode_NoInherits_EmptyChain() {
        var graph = new InheritanceGraph();
        var node = MakeNode("Item", null);
        graph.AddNode(node);
        node.InheritChain.Should().BeEmpty();
        graph.FlattenedFields(node).Should().BeEmpty();
    }

    [Fact]
    public void InheritsSchema_ResolvesChain() {
        var graph = new InheritanceGraph();
        graph.AddSchema(MakeSchema("Weapon", [("Damage", "int")]));
        var node = MakeNode("Sword", "Weapon");
        graph.AddNode(node);
        node.InheritChain.Should().ContainSingle().Which.Should().Be("Weapon");
    }

    [Fact]
    public void FlattenedFields_MergesParentAndChild() {
        var graph = new InheritanceGraph();
        graph.AddSchema(MakeSchema("Base", [("Id", "int")]));
        var node = MakeNode("Derived", "Base", [("Name", "string")]);
        graph.AddNode(node);
        var fields = graph.FlattenedFields(node);
        fields.Select(f => f.Name).Should().BeEquivalentTo(["Id", "Name"]);
    }

    [Fact]
    public void ChildOverride_ReplacesParentField() {
        var graph = new InheritanceGraph();
        graph.AddSchema(MakeSchema("Base", [("Value", "int")]));
        var node = MakeNode("Derived", "Base", [("Value", "float")]);
        graph.AddNode(node);
        var fields = graph.FlattenedFields(node);
        fields.Should().ContainSingle(f => f.Name == "Value");
        fields.First(f => f.Name == "Value").Type.Should().Be("float");
    }

    [Fact]
    public void DeepChain_FlattensAll() {
        var graph = new InheritanceGraph();
        graph.AddSchema(MakeSchema("A", [("A1", "int")]));
        graph.AddNode(MakeNode("B", "A", [("B1", "int")]));
        graph.AddNode(MakeNode("C", "B", [("C1", "int")]));
        var fields = graph.FlattenedFields(graph.Nodes["C"]);
        fields.Select(f => f.Name).Should().BeEquivalentTo(["A1", "B1", "C1"]);
    }

    [Fact]
    public void Cycle_DoesNotInfiniteLoop() {
        var graph = new InheritanceGraph();
        var a = MakeNode("A", "B");
        var b = MakeNode("B", "A");
        graph.AddNode(a);
        graph.AddNode(b);
        // Should not throw
        a.InheritChain.Length.Should().BeLessThan(10);
    }

    private static DataFileDefinition MakeNode(string name, string? inherits,
        (string Name, string Type)[]? fields = null) {
        var flds = (fields ?? []).Select(f => new FieldDefinition(f.Name, f.Type)).ToImmutableArray();
        return new DataFileDefinition(name, "", $"{name}.json", inherits, flds,
            ImmutableDictionary<string, object?>.Empty);
    }

    private static SchemaDefinition MakeSchema(string name, (string Name, string Type)[] fields) {
        return new SchemaDefinition(name, "", $"{name}.json",
            fields.Select(f => new FieldDefinition(f.Name, f.Type)).ToImmutableArray());
    }
}
