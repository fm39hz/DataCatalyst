using System.Collections.Immutable;
using FluentAssertions;
using DataCatalyst.DataRoot;
using Xunit;

namespace DataCatalyst.Tests;

public class DataRootScannerTests {
    [Fact]
    public void ScanSchema_ProducesSchemaDefinition() {
        var json = """{ "kind": "Weapon", "fields": { "Damage": { "type": "int" } } }""";
        var scanner = new DataRootScanner();
        scanner.Scan("Data/", [("Data/_weapon.json", json)]);
        scanner.Schemas.Should().ContainSingle();
        scanner.Schemas[0].Name.Should().Be("Weapon");
        scanner.Schemas[0].Fields.Should().ContainSingle(f => f.Name == "Damage" && f.Type is IntFieldType);
    }

    [Fact]
    public void ScanDataFile_ProducesDataFileDefinition() {
        var json = """{ "inherits": "Weapon", "defaults": { "Damage": 25 }, "load": "compile" }""";
        var scanner = new DataRootScanner();
        scanner.Scan("Data/", [("Data/sword.json", json)]);
        scanner.DataFiles.Should().ContainSingle();
        var file = scanner.DataFiles[0];
        file.Name.Should().Be("sword");
        file.Inherits.Should().Be("Weapon");
        file.IsCompileEager.Should().BeTrue();
        file.Defaults["Damage"].Should().Be(25);
    }

    [Fact]
    public void ScanDataFile_StartupByDefault() {
        var json = """{ "defaults": { "X": 1 } }""";
        var scanner = new DataRootScanner();
        scanner.Scan("Data/", [("Data/item.json", json)]);
        scanner.DataFiles[0].IsCompileEager.Should().BeFalse();
    }

    [Fact]
    public void FileOutsideRoot_IsIgnored() {
        var scanner = new DataRootScanner();
        scanner.Scan("Data/", [("Other/outside.json", """{ "kind": "X" }""")]);
        scanner.Schemas.Should().BeEmpty();
        scanner.DataFiles.Should().BeEmpty();
    }

    [Fact]
    public void ScanFiltersByRootPrefix() {
        var scanner = new DataRootScanner();
        scanner.Scan("Data/", [
            ("Data/_weapon.json", """{ "kind": "Weapon" }"""),
            ("Config/settings.json", """{ "kind": "Settings" }"""),
        ]);
        scanner.Schemas.Should().ContainSingle();
        scanner.Schemas[0].Name.Should().Be("Weapon");
    }
}
