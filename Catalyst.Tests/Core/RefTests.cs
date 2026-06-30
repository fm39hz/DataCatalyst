namespace Catalyst.Tests.Core;

using Xunit;

public class RefTests {
	public struct TestConcept : IConcept { }

	[Fact]
	public void Constructor_AssignsType() {
		var r = new Ref<TestConcept>(typeof(TestConcept));
		Assert.Equal(typeof(TestConcept), r.BeingType);
		Assert.True(r.IsValid);
	}

	[Fact]
	public void Constructor_NullType_Throws() => Assert.Throws<ArgumentNullException>(() => new Ref<TestConcept>(null!));

	[Fact]
	public void Default_IsInvalid() {
		Ref<TestConcept> r = default;
		Assert.Null(r.BeingType);
		Assert.False(r.IsValid);
	}

	[Fact]
	public void ToString_ReturnsTypeName() {
		var r = new Ref<TestConcept>(typeof(TestConcept));
		Assert.Equal("TestConcept", r.ToString());
	}

	[Fact]
	public void ToString_DefaultReturnsNone() {
		Ref<TestConcept> r = default;
		Assert.Equal("None", r.ToString());
	}
}
