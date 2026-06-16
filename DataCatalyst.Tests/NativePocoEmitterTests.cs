using System.Collections.Immutable;
using FluentAssertions;
using DataCatalyst.DataRoot;
using Xunit;

namespace DataCatalyst.Tests;

public class NativePocoEmitterTests {
    [Fact]
    public void EmitCompileEager_ProducesStaticReadonly() {
        var (graph, _) = BuildGraph("compile");
        var emitter = new NativePocoEmitter(graph, "Data");
        var code = emitter.EmitAll();
        code.Should().Contain("public static readonly TestData TestData = new TestData { X = 1");
        code.Should().NotContain("Initialize");
        code.Should().NotContain("DataContextRegistry");
    }

    [Fact]
    public void EmitStartup_ProducesStaticPropertyAndInitialize() {
        var (graph, _) = BuildGraph("startup");
        var emitter = new NativePocoEmitter(graph, "Data");
        var code = emitter.EmitAll();
        code.Should().Contain("public static TestData TestData { get; private set; }");
        code.Should().Contain("DataContextRegistry.Register(Initialize)");
        code.Should().Contain("static void Initialize");
        code.Should().Contain("overrides is not null");
    }

    [Fact]
    public void EmitStartup_IncludesOverrideSwitch() {
        var (graph, node) = BuildGraph("startup", f => f.Add(new FieldDefinition("Y", "int")));
        var emitter = new NativePocoEmitter(graph, "Data");
        var code = emitter.EmitAll();
        code.Should().Contain("case \"TestData\"");
        code.Should().Contain("TryGetValue(\"X\", out var _vX)");
    }

    [Fact]
    public void Emit_MultipleNodes_AllEmitted() {
        var graph = new InheritanceGraph();
        graph.AddNode(MakeNode("A", null, ("X", "int")));
        graph.AddNode(MakeNode("B", null, ("Y", "float"), isCompile: true));
        var emitter = new NativePocoEmitter(graph, "Root");
        var code = emitter.EmitAll();
        code.Should().Contain("struct A");
        code.Should().Contain("struct B");
        code.Should().Contain("readonly B B"); // compile-eager
        code.Should().NotContain("readonly A A"); // startup
    }

    [Fact]
    public void Emit_NamespaceCorrect() {
        var (graph, _) = BuildGraph("startup");
        var emitter = new NativePocoEmitter(graph, "Game.Items");
        var code = emitter.EmitAll();
        code.Should().Contain("namespace Game.Items");
        code.Should().Contain("partial class ItemsContext");
    }

    [Fact]
    public void Emit_EmptyGraph_NoStructs() {
        var emitter = new NativePocoEmitter(new InheritanceGraph(), "Empty");
        var code = emitter.EmitAll();
        code.Should().NotContain("struct");
        code.Should().Contain("namespace Empty");
    }

    private static (InheritanceGraph Graph, DataFileDefinition Node) BuildGraph(string load,
        Action<ImmutableArray<FieldDefinition>.Builder>? extraFields = null) {
        var graph = new InheritanceGraph();
        var fb = ImmutableArray.CreateBuilder<FieldDefinition>();
        fb.Add(new FieldDefinition("X", "int"));
        if (extraFields is not null) extraFields(fb);
        graph.AddNode(new DataFileDefinition("TestData", "", "test.json", null,
            fb.ToImmutable(), ImmutableDictionary<string, object?>.Empty
                .Add("X", 1), isCompileEager: load == "compile"));
        return (graph, graph.Nodes["TestData"]);
    }

    private static DataFileDefinition MakeNode(string name, string? inherits,
        (string Name, string Type) field, bool isCompile = false) {
        return new DataFileDefinition(name, "", $"{name}.json", inherits,
            [new FieldDefinition(field.Name, field.Type)],
            ImmutableDictionary<string, object?>.Empty, isCompileEager: isCompile);
    }
}
