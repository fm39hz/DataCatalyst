using FluentAssertions;
using DataCatalyst.Runtime;
using Xunit;

namespace DataCatalyst.Tests;

public class DataMergeTests {
    [Fact]
    public void DataSource_Priority_DefaultsToZero() {
        var src = DataSource.From("path/");
        src.Priority.Should().Be(0);
        src.Name.Should().Be("path");
        src.Directory.Should().Be("path/");
    }

    [Fact]
    public void DataSource_WithPriority() {
        var src = DataSource.From("Mods/", priority: 10);
        src.Priority.Should().Be(10);
    }
}
