namespace Catalyst.Tests.Core;

using Catalyst.Storage;
using FluentAssertions;
using Xunit;

public class StorageTests {
	private struct S_TestComponentA { public int Value; }

	public class DynamicPoolTests {
		[Fact]
		public void Resize_Grows() {
			var pool = new DynamicPool();
			pool.Resize(5);
			pool.Count.Should().Be(5);
		}

		[Fact]
		public void Resize_DoesNotShrink() {
			var pool = new DynamicPool();
			pool.Resize(5);
			pool.Resize(3);
			pool.Count.Should().Be(5);
		}

		[Fact]
		public void Count_ReflectsSize() {
			var pool = new DynamicPool();
			pool.Count.Should().Be(0);
			pool.Resize(3);
			pool.Count.Should().Be(3);
		}

		[Fact]
		public void Set_And_Get_Work() {
			var pool = new DynamicPool();
			pool.Resize(1);
			pool.Set(0, new S_TestComponentA { Value = 42 });
			var val = pool.Get<S_TestComponentA>(0);
			val.Value.Should().Be(42);
		}

		[Fact]
		public void Get_Throws_OnInvalidIndex() {
			var pool = new DynamicPool();
			pool.Invoking(p => p.Get<S_TestComponentA>(-1))
				.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void Get_Throws_OnMissingType() {
			var pool = new DynamicPool();
			pool.Resize(1);
			pool.Invoking(p => p.Get<S_TestComponentA>(0))
				.Should().Throw<KeyNotFoundException>();
		}

		[Fact]
		public void SetRaw_And_Get_Work() {
			var pool = new DynamicPool();
			pool.Resize(1);
			pool.SetRaw(0, typeof(S_TestComponentA), new S_TestComponentA { Value = 99 });
			var val = pool.Get<S_TestComponentA>(0);
			val.Value.Should().Be(99);
		}

		[Fact]
		public void SetRawValue_And_GetRaw_Work() {
			var pool = new DynamicPool();
			pool.Resize(1);
			pool.SetRawValue(0, 42, 100);
			pool.GetRaw(0, 42).Should().Be(100);
		}

		[Fact]
		public void SetRawValue_Overwrites() {
			var pool = new DynamicPool();
			pool.Resize(1);
			pool.SetRawValue(0, 42, 100);
			pool.SetRawValue(0, 42, 200);
			pool.GetRaw(0, 42).Should().Be(200);
		}
	}

	public class GenericPoolTests {
		[Fact]
		public void Resize_Grows() {
			var pool = new GenericPool();
			pool.Resize(5);
			pool.Count.Should().Be(5);
		}

		[Fact]
		public void Set_And_Get_Work() {
			var pool = new GenericPool();
			pool.Resize(1);
			pool.Set(0, new S_TestComponentA { Value = 42 });
			var val = pool.Get<S_TestComponentA>(0);
			val.Value.Should().Be(42);
		}

		[Fact]
		public void SetRaw_And_Get_Work() {
			var pool = new GenericPool();
			pool.Resize(1);
			pool.SetRaw(0, typeof(S_TestComponentA), new S_TestComponentA { Value = 77 });
			var val = pool.Get<S_TestComponentA>(0);
			val.Value.Should().Be(77);
		}

		[Fact]
		public void Get_Throws_OnInvalidIndex() {
			var pool = new GenericPool();
			pool.Invoking(p => p.Get<S_TestComponentA>(-1))
				.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void Get_Throws_OnMissingType() {
			var pool = new GenericPool();
			pool.Resize(1);
			pool.Invoking(p => p.Get<S_TestComponentA>(0))
				.Should().Throw<KeyNotFoundException>();
		}
	}
}
