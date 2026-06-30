namespace Catalyst.Tests.Core;

using Catalyst.Loaders;
using Catalyst.Storage;
using FluentAssertions;
using Xunit;

public class JsonDataLoaderTests {
    [Fact]
    public void Load_WithObjectRoot_ExtractsBeings() {
        var loader = new JsonDataLoader();
        var result = loader.Load("""{ "Hero": { "Health": { "Current": 100 } } }""", "test");
        result.Beings.Should().HaveCount(1);
    }

    [Fact]
    public void Load_WithArrayRoot_ExtractsBeings() {
        var loader = new JsonDataLoader();
        var result = loader.Load("""[{ "$key": "Hero", "Health": { "Current": 100 } }]""", "test");
        result.Beings.Should().HaveCount(1);
    }

    [Fact]
    public void Load_ExtractsKey() {
        var loader = new JsonDataLoader();
        var result = loader.Load("""{ "Hero": { "Health": { "Current": 100 } } }""", "test");
        result.Beings.Should().HaveCount(1);
        ((RawBeing)result.Beings[0]!).Key.Should().Be("Hero");
    }

    [Fact]
    public void Load_ExtractsDollarKey() {
        var loader = new JsonDataLoader();
        var result = loader.Load("""[{ "$key": "ChosenOne", "Health": { "Current": 100 } }]""", "test");
        result.Beings.Should().HaveCount(1);
        ((RawBeing)result.Beings[0]!).Key.Should().Be("ChosenOne");
    }

    [Fact]
    public void Load_DetectsDuplicateKeys() {
        var loader = new JsonDataLoader();
        var result = loader.Load("""[{ "$key": "Hero", "Health": 100 }, { "$key": "Hero", "Mana": 50 }]""", "test");
        result.Beings.Should().HaveCount(1);
        result.Diagnostics.Should().Contain(d => d.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadFile_LoadsJsonFile() {
        var loader = new JsonDataLoader();
        var path = Path.GetTempFileName() + ".json";
        try {
            File.WriteAllText(path, """{ "TestBeing": { "Health": 100 } }""");
            var result = loader.LoadFile(path);
            result.Beings.Should().HaveCount(1);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadDirectory_LoadsAllJson() {
        var loader = new JsonDataLoader();
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try {
            File.WriteAllText(Path.Combine(dir, "a.json"), """{ "BeingA": { "Health": 100 } }""");
            File.WriteAllText(Path.Combine(dir, "b.json"), """{ "BeingB": { "Health": 50 } }""");
            var result = loader.LoadDirectory(dir);
            result.Beings.Should().HaveCount(2);
        } finally {
            Directory.Delete(dir, true);
        }
    }
}
