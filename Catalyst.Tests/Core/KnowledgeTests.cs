namespace Catalyst.Tests.Core;

using Catalyst.Knowledge;
using Catalyst.Schema;
using Catalyst.Storage;
using FluentAssertions;
using Xunit;

public class KnowledgeTests {
    struct K_TestBeingA : IBeing { }
    struct K_TestConceptFoo : IConcept { }
    struct K_TestAspectX : IRevealedBy<K_TestConceptFoo> { public int Value; }

    static Knowledge CreateKnowledge(
        Dictionary<Type, ITypedStoragePool>? pools = null,
        Dictionary<Type, int>? beingIndices = null,
        SchemaRegistry? schema = null)
    {
        return new Knowledge(
            pools ?? [],
            beingIndices ?? [],
            schema);
    }

    [Fact]
    public void GetPool_ReturnsNull_ForUnknownConcept() {
        var k = CreateKnowledge();
        k.GetPool(typeof(K_TestConceptFoo)).Should().BeNull();
    }

    [Fact]
    public void GetPool_ReturnsPool_ForKnownConcept() {
        var pool = new GenericPool();
        var k = CreateKnowledge(pools: new() { { typeof(K_TestConceptFoo), pool } });
        k.GetPool(typeof(K_TestConceptFoo)).Should().BeSameAs(pool);
    }

    [Fact]
    public void Of_Throws_WhenPoolMissing() {
        var k = CreateKnowledge(beingIndices: new() { { typeof(K_TestBeingA), 0 } });
        k.Invoking(k => k.Of<K_TestConceptFoo, K_TestAspectX>(typeof(K_TestBeingA)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Of_Throws_WhenNotIndexed() {
        var pool = new GenericPool();
        pool.Resize(1);
        pool.Set(0, new K_TestAspectX { Value = 42 });
        var k = CreateKnowledge(pools: new() { { typeof(K_TestConceptFoo), pool } });
        k.Invoking(k => k.Of<K_TestConceptFoo, K_TestAspectX>(typeof(K_TestBeingA)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Get_Throws_WhenPoolMissing() {
        var k = CreateKnowledge(beingIndices: new() { { typeof(K_TestBeingA), 0 } });
        k.Invoking(k => k.Get<K_TestAspectX>(typeof(K_TestConceptFoo), typeof(K_TestBeingA)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Get_Throws_WhenNotIndexed() {
        var pool = new GenericPool();
        pool.Resize(1);
        pool.Set(0, new K_TestAspectX { Value = 42 });
        var k = CreateKnowledge(pools: new() { { typeof(K_TestConceptFoo), pool } });
        k.Invoking(k => k.Get<K_TestAspectX>(typeof(K_TestConceptFoo), typeof(K_TestBeingA)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void About_Throws_WhenBeingNotFound() {
        var k = CreateKnowledge();
        k.Invoking(k => k.About<K_TestAspectX>(typeof(K_TestBeingA)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void About_Throws_WhenAspectNotFound() {
        var pool = new GenericPool();
        pool.Resize(1);
        var k = CreateKnowledge(
            pools: new() { { typeof(K_TestConceptFoo), pool } },
            beingIndices: new() { { typeof(K_TestBeingA), 0 } });
        k.Invoking(k => k.About<K_TestAspectX>(typeof(K_TestBeingA)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetDynamicPool_ReturnsNull_ForUnknownConcept() {
        var k = CreateKnowledge();
        k.GetDynamicPool("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetDynamicBeingIndex_ReturnsMinusOne_ForUnknownBeing() {
        var k = CreateKnowledge();
        k.GetDynamicBeingIndex("unknown").Should().Be(-1);
    }

    [Fact]
    public void GetDynamicConceptNames_ReturnsAllNames() {
        var k = CreateKnowledge();
        var pool = new DynamicPool();
        k.SetDynamicPools(
            new() { { "TestConcept", pool } },
            new() { { "being1", 0 }, { "being2", 1 } });

        k.GetDynamicConceptNames().Should().BeEquivalentTo(["TestConcept"]);
        k.GetDynamicPool("TestConcept").Should().BeSameAs(pool);
        k.GetDynamicBeingIndex("being1").Should().Be(0);
        k.GetDynamicBeingIndex("being2").Should().Be(1);
    }
}
