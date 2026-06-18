using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DataCatalyst.Core;
using DataCatalyst.Loaders;
using FluentAssertions;
using Xunit;

namespace DataCatalyst.Tests {
	public struct GameComponent : IComponent {
		public int Value { get; set; }
	}
} // namespace DataCatalyst.Tests

namespace ModNamespace {
	using DataCatalyst.Tests;

	public struct GameComponent : IComponent {
		public string Name { get; set; }
	}
}

namespace DataCatalyst.Tests {
	public class JsonDataLoaderTests : IDisposable {
		private readonly string _tempDir;
		private readonly DataCatalystEnvironment _env;

		public JsonDataLoaderTests() {
			_tempDir = Path.Combine(Path.GetTempPath(), "DataCatalystTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
			_env = new DataCatalystEnvironment();
		}

		public void Dispose() {
			if (Directory.Exists(_tempDir)) {
				Directory.Delete(_tempDir, true);
			}

			GC.SuppressFinalize(this);
		}

		private static JsonSerializerOptions CreateAotOptions() => new() {
			TypeInfoResolver = new DefaultJsonTypeInfoResolver()
		};

		[Fact]
		public void LoadDirectory_WithSerializerOptions_Succeeds() {
			// Arrange
			_env.Primitives.Register<GameComponent>();
			var jsonPath = Path.Combine(_tempDir, "hero.json");
			File.WriteAllText(jsonPath, /*lang=json,strict*/ @"{
			""GameComponent"": {
				""Value"": 123
			}
		}");

			var options = CreateAotOptions();

			// Act
			var result = JsonDataLoader.LoadDirectory(_tempDir, options, _env);

			// Assert
			result.Diagnostics.Should().BeEmpty();
			result.Entries.Should().ContainSingle();
			var entry = result.Entries[0];
			entry.Key.Should().Be("hero");
			entry.Get<GameComponent>().Value.Should().Be(123);
		}

		[Fact]
		public void LoadDirectory_TypeNameAsDiscriminator_ResolvesCorrectly() {
			// Arrange
			_env.Primitives.Register<GameComponent>();

			var jsonPath = Path.Combine(_tempDir, "hero.json");
			File.WriteAllText(jsonPath, /*lang=json,strict*/ @"{
			""GameComponent"": {
				""Value"": 456
			}
		}");

			var options = CreateAotOptions();

			// Act
			var result = JsonDataLoader.LoadDirectory(_tempDir, options, _env);

			// Assert
			result.Diagnostics.Should().BeEmpty();
			result.Entries.Should().ContainSingle();
			result.Entries[0].Get<GameComponent>().Value.Should().Be(456);
		}

		[Fact]
		public void LoadDirectory_RecordsDiagnosticsForMalformedJson() {
			// Arrange
			_env.Primitives.Register<GameComponent>();
			var jsonPath = Path.Combine(_tempDir, "malformed.json");
			File.WriteAllText(jsonPath, "{ invalid json }");

			var options = CreateAotOptions();

			// Act
			var result = JsonDataLoader.LoadDirectory(_tempDir, options, _env);

			// Assert
			result.Entries.Should().BeEmpty();
			result.Diagnostics.Should().ContainSingle(d => d.Contains("Failed to load file"));
		}

		[Fact]
		public void DataGraphBuilder_DuplicateKeys_MergesComponentsDeterministically() {
			// Arrange
			_env.Primitives.Register<GameComponent>();
			_env.Primitives.Register<OtherStruct>();

			var entries = new List<DataEntry>();

			// Base entry
			var baseEntry = new DataEntry("enemy", new() {
				[typeof(GameComponent)] = new GameComponent { Value = 10 }
			});
			baseEntry.SourceFile = "Base/enemy.json";
			entries.Add(baseEntry);

			// Mod entry overriding base entry component and adding another component
			var modEntry = new DataEntry("enemy", new() {
				[typeof(GameComponent)] = new GameComponent { Value = 25 },
				[typeof(OtherStruct)] = new OtherStruct { Y = 99 }
			});
			modEntry.SourceFile = "Mod/enemy.json";
			entries.Add(modEntry);

			var diagnostics = new List<string>();

			// Act
			var graph = DataGraphBuilder.Build(entries, diagnostics, _env);

			// Assert
			diagnostics.Should().ContainSingle(d => d.Contains("overrides/merges components of existing entry"));
			graph.Entries.Should().ContainKey("enemy");

			var resolvedEntry = graph.Entries["enemy"];
			resolvedEntry.Get<GameComponent>().Value.Should().Be(25);
			resolvedEntry.Get<OtherStruct>().Y.Should().Be(99);
		}

		[Fact]
		public void LoadDirectory_WithTypeNameDiscriminator_Succeeds() {
			// Arrange
			_env.Primitives.Register<GameComponent>();

			var jsonPath = Path.Combine(_tempDir, "hero.json");
			File.WriteAllText(jsonPath, /*lang=json,strict*/ @"{
			""GameComponent"": {
				""Value"": 123
			}
		}");

			var options = CreateAotOptions();

			// Act
			var result = JsonDataLoader.LoadDirectory(_tempDir, options, _env);

			// Assert
			result.Diagnostics.Should().BeEmpty();
			result.Entries.Should().ContainSingle();
			var entry = result.Entries[0];
			entry.Key.Should().Be("hero");
			entry.Get<GameComponent>().Value.Should().Be(123);
		}
	}
} // namespace DataCatalyst.Tests
