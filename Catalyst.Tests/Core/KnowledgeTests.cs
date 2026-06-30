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
    sealed class K_TestBaked { public string? Info { get; set; } }

    static Knowledge CreateKnowledge(
        Dictionary<Type, ITypedStoragePool>? pools = null,
        Dictionary<Type, int>? beingIndices = null,
        SchemaRegistry? schema = null,
        Dictionary<Type, Dictionary<string, object>>? bakedCache = null)
    {
        return new Knowledge(
            pools ?? [],
            beingIndices ?? [],
            schema,
            bakedCache ?? []);
    }

    [Fact]
    public void GetBeingIndex_ReturnsMinusOne_ForUnknownType() {
        var k = CreateKnowledge();
        k.GetBeingIndex(typeof(string)).Should().Be(-1);
    }

    [Fact]
    public void GetBeingIndex_ReturnsCorrectIndex_ForKnownType() {
        var k = CreateKnowledge(beingIndices: new() { { typeof(K_TestBeingA), 5 } });
        k.GetBeingIndex(typeof(K_TestBeingA)).Should().Be(5);
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
    public void GetBaked_Throws_WhenKeyMissing() {
        var k = CreateKnowledge();
        k.Invoking(k => k.GetBaked<K_TestBaked>("missing"))
            .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TryGetBaked_ReturnsFalse_WhenKeyMissing() {
        var k = CreateKnowledge();
        k.TryGetBaked<K_TestBaked>("missing", out var _).Should().BeFalse();
    }

    [Fact]
    public void TryGetBaked_ReturnsTrue_WhenKeyFound() {
        var baked = new Dictionary<Type, Dictionary<string, object>> {
            { typeof(K_TestBaked), new() { { "hero", new K_TestBaked { Info = "data" } } } }
        };
        var k = CreateKnowledge(bakedCache: baked);
        k.TryGetBaked<K_TestBaked>("hero", out var result).Should().BeTrue();
        result.Info.Should().Be("data");
    }

    [Fact]
    public void GetBaked_ReturnsAll_WhenTypeExists() {
        var baked = new Dictionary<Type, Dictionary<string, object>> {
            { typeof(K_TestBaked), new() { { "a", new K_TestBaked { Info = "A" } }, { "b", new K_TestBaked { Info = "B" } } } }
        };
        var k = CreateKnowledge(bakedCache: baked);
        var all = k.GetBaked<K_TestBaked>();
        all.Should().HaveCount(2);
        all["a"].Info.Should().Be("A");
        all["b"].Info.Should().Be("B");
    }

    [Fact]
    public void GetBaked_ReturnsEmpty_WhenTypeMissing() {
        var k = CreateKnowledge();
        k.GetBaked<K_TestBaked>().Should().BeEmpty();
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
