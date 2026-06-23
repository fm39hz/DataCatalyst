namespace DataCatalyst.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataCatalyst.Abstractions;
using DataCatalyst.Core;
using CoreConcept = DataCatalyst.Core.Concept;
using FluentAssertions;
using Xunit;

public class DataSourceTests {
	[Fact]
	public void LoadOrderResolver_SortsByDependencies() {
		var loader = new MockDataLoader();
		var sources = new List<DataSource> {
			new DataSource("ModB", loader, "Mods/ModB") { DependsOn = new[] { "ModA" } },
			new DataSource("ModA", loader, "Mods/ModA") { DependsOn = new[] { "Base" } },
			new DataSource("Base", loader, "Data/Base")
		};

		var diags = new List<string>();
		var sorted = LoadOrderResolver.Resolve(sources, diags);

		diags.Should().BeEmpty();
		sorted.Select(s => s.Name).Should().ContainInOrder("Base", "ModA", "ModB");
	}

	[Fact]
	public void LoadOrderResolver_DetectsCycles() {
		var loader = new MockDataLoader();
		var sources = new List<DataSource> {
			new DataSource("ModA", loader, "Mods/ModA") { DependsOn = new[] { "ModB" } },
			new DataSource("ModB", loader, "Mods/ModB") { DependsOn = new[] { "ModA" } }
		};

		var diags = new List<string>();
		var sorted = LoadOrderResolver.Resolve(sources, diags);

		diags.Should().Contain(d => d.Contains("Cycle detected"));
		sorted.Should().HaveCount(2); // Should still return all to prevent loss
	}

	[Fact]
	public void LoadOrderResolver_TieBreaksDeterministic() {
		var loader = new MockDataLoader();
		// Independent sources ModX, ModY, ModZ. Sort order should be Priority, then Name.
		var sources = new List<DataSource> {
			new DataSource("ModZ", loader, "Mods/ModZ") { Priority = 1 },
			new DataSource("ModY", loader, "Mods/ModY") { Priority = 2 },
			new DataSource("ModX", loader, "Mods/ModX") { Priority = 1 }
		};

		var sorted = LoadOrderResolver.Resolve(sources);
		sorted.Select(s => s.Name).Should().ContainInOrder("ModX", "ModZ", "ModY");
	}

	[Fact]
	public void PolicyGraphBuilder_ApplyScopeFilter() {
		var loader = new MockDataLoader();
		var entry1 = new DataEntry("Goblin", new() {
			[typeof(CoreConcept)] = new CoreConcept { Value = new[] { "Enemy" } },
			[typeof(TestStruct)] = new TestStruct { X = 100 }
		});
		var entry2 = new DataEntry("Sword", new() {
			[typeof(CoreConcept)] = new CoreConcept { Value = new[] { "Item" } },
			[typeof(TestStruct)] = new TestStruct { X = 200 }
		});

		loader.AddDirectory("Data/Base", new() { entry1, entry2 });

		var pipeline = new DataPipeline()
			.AddSource(new DataSource("Base", loader, "Data/Base") {
				Scope = new[] { "Enemy" } // Only authority over Enemy
			});

		var catalog = pipeline.Build();

		catalog.Entries.Should().ContainKey("Goblin");
		catalog.Entries.Should().NotContainKey("Sword");
	}

	[Fact]
	public void PolicyGraphBuilder_ApplyFieldPatch() {
		// Register mergers manually in ComponentMerger to simulate SourceGen outputs
		ComponentMerger.Register<TestStruct>((curr, inh) => {
			var c = (TestStruct)curr;
			var i = (TestStruct)inh;
			// Rule: incoming (current) wins non-default, existing (inherited) fills default
			return new TestStruct {
				X = c.X != 0 ? c.X : i.X,
				Y = c.Y != 0 ? c.Y : i.Y
			};
		});

		var loader = new MockDataLoader();
		var baseEntry = new DataEntry("Hero", new() {
			[typeof(TestStruct)] = new TestStruct { X = 100, Y = 100 }
		});
		var modEntry = new DataEntry("Hero", new() {
			[typeof(TestStruct)] = new TestStruct { X = 0, Y = 999 } // X is default (0), Y is modified (999)
		});

		loader.AddDirectory("Data/Base", new() { baseEntry });
		loader.AddDirectory("Mods/ModA", new() { modEntry });

		// Pipeline using FieldPatch
		var pipeline = new DataPipeline()
			.AddSource(new DataSource("Base", loader, "Data/Base") { Priority = 0 })
			.AddSource(new DataSource("ModA", loader, "Mods/ModA") { Priority = 1, MergePolicy = MergePolicy.FieldPatch });

		var catalog = pipeline.Build();

		// TODO: In a future version, generate shadow tracking structs to distinguish explicit defaults from uninitialized defaults.
		// For now, if a field is 0 (default), it gets filled by the base's value (100).
		catalog.Get<TestStruct>(catalog.GetEntryId("Hero")).X.Should().Be(100);
		catalog.Get<TestStruct>(catalog.GetEntryId("Hero")).Y.Should().Be(999);
	}

	[Fact]
	public void PolicyGraphBuilder_ApplyOverlay() {
		// ComponentMerger needs to be registered
		ComponentMerger.Register<TestStruct>((curr, inh) => {
			var c = (TestStruct)curr;
			var i = (TestStruct)inh;
			return new TestStruct {
				X = c.X != 0 ? c.X : i.X,
				Y = c.Y != 0 ? c.Y : i.Y
			};
		});

		var loader = new MockDataLoader();
		var baseEntry = new DataEntry("Dragon", new() {
			[typeof(TestStruct)] = new TestStruct { X = 100, Y = 100 }
		});
		var overlayEntry = new DataEntry("Dragon", new() {
			[typeof(TestStruct)] = new TestStruct { Y = 888 }
		});

		loader.AddDirectory("Data/Base", new() { baseEntry });
		loader.AddDirectory("Localization/VI", new() { overlayEntry });

		var pipeline = new DataPipeline()
			.AddSource(new DataSource("Base", loader, "Data/Base") { Priority = 0 })
			.AddSource(new DataSource("LangVI", loader, "Localization/VI") { Priority = 1, MergePolicy = MergePolicy.Overlay });

		var catalog = pipeline.Build();

		var heroId = catalog.GetEntryId("Dragon");
		catalog.Get<TestStruct>(heroId).X.Should().Be(100);
		catalog.Get<TestStruct>(heroId).Y.Should().Be(888);
	}

	public struct TestStruct {
		public int X { get; set; }
		public int Y { get; set; }
	}

	private class MockDataLoader : IDataLoader {
		private readonly Dictionary<string, List<DataEntry>> _entriesByPath = new(StringComparer.Ordinal);

		public void AddDirectory(string path, List<DataEntry> entries) {
			_entriesByPath[path] = entries;
		}

		public LoadResult LoadDirectory(string path) {
			var result = new LoadResult();
			if (_entriesByPath.TryGetValue(path, out var list)) {
				result._entries.AddRange(list);
			}
			return result;
		}

		public LoadResult LoadFile(string path) => LoadDirectory(Path.GetDirectoryName(path));
	}
}
