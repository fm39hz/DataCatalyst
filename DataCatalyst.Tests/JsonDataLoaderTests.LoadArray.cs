namespace DataCatalyst.Tests;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Core;
using DataCatalyst.Loaders;
using FluentAssertions;
using Xunit;

public class JsonDataLoaderLoadArrayTests : IDisposable {
	private readonly string _tempDir;

	public JsonDataLoaderLoadArrayTests() {
		_tempDir = Path.Combine(Path.GetTempPath(), "DataCatalystArrayTests_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
		PrimitiveRegistry.Clear();
	}

	public void Dispose() {
		if (Directory.Exists(_tempDir)) {
			Directory.Delete(_tempDir, true);
		}

		PrimitiveRegistry.Clear();
		GC.SuppressFinalize(this);
	}

	private static JsonSerializerOptions CreateAotOptions() => new() {
		TypeInfoResolver = new DefaultJsonTypeInfoResolver()
	};

	[Fact]
	public void LoadArray_Aot_Succeeds() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();
		var filePath = Path.Combine(_tempDir, "substances.json");
		File.WriteAllText(filePath, /*lang=json,strict*/ @"[
			{
				""id"": ""H2O"",
				""GameComponent"": {
					""Value"": 18
				}
			},
			{
				""id"": ""O2"",
				""inherits"": [""H2O""],
				""GameComponent"": {
					""Value"": 32
				}
			}
		]");

		var options = CreateAotOptions();

		// Act
		var result = JsonDataLoader.LoadArray(filePath, "id", options);

		// Assert
		result.Diagnostics.Should().BeEmpty();
		result.Entries.Count.Should().Be(2);

		var entry1 = result.Entries[0];
		entry1.Key.Should().Be("H2O");
		entry1.Get<GameComponent>().Value.Should().Be(18);

		var entry2 = result.Entries[1];
		entry2.Key.Should().Be("O2");
		entry2.Inherits.Should().ContainSingle().Which.Should().Be("H2O");
		entry2.Get<GameComponent>().Value.Should().Be(32);
	}

	[Fact]
	public void LoadArrayReflection_Succeeds() {
		// Arrange
		PrimitiveRegistry.Register<GameComponent>();
		var filePath = Path.Combine(_tempDir, "substances.json");
		File.WriteAllText(filePath, /*lang=json,strict*/ @"[
			{
				""id"": ""CO2"",
				""GameComponent"": {
					""Value"": 44
				}
			}
		]");

		// Act
#pragma warning disable CS0618 // Type or member is obsolete
		var result = JsonDataLoader.LoadArrayReflection(filePath, "id");
#pragma warning restore CS0618

		// Assert
		result.Diagnostics.Should().BeEmpty();
		result.Entries.Count.Should().Be(1);

		var entry = result.Entries[0];
		entry.Key.Should().Be("CO2");
		entry.Get<GameComponent>().Value.Should().Be(44);
	}
}
