namespace UniversalDataDriven.Test.Unit;

using FluentAssertions;
using UniversalDataDriven.Abstractions;
using UniversalDataDriven.Core;
using UniversalDataDriven.Test.Support;
using Xunit;

public sealed class DecimalPrimitivePluginTests {
	[Fact]
	public void Registry_ShouldDiscoverDecimalPlugin_ViaModuleInitializer() {
		var decimalRule = UddsgPluginRegistry.Primitives
			.FirstOrDefault(r => r.Name == "decimal");

		decimalRule.Should().NotBeNull();
		decimalRule!.Should().BeOfType<DecimalPrimitiveRule>();
	}

	[Fact]
	public void DecimalPlugin_ShouldOutrankFloat_ProvingOcp() {
		var floatRule = UddsgPluginRegistry.Primitives.First(r => r.Name == "float");
		var decimalRule = UddsgPluginRegistry.Primitives.First(r => r.Name == "decimal");

		decimalRule.Rank.Should().BeGreaterThan(floatRule.Rank);
	}

	[Fact]
	public void DecimalPlugin_ShouldEmitDecimalLiteralSyntax() {
		var rule = UddsgPluginRegistry.Primitives.First(r => r.Name == "decimal");
		using var doc = System.Text.Json.JsonDocument.Parse("3.14159");
		var value = JsonValueModel.From(doc.RootElement);

		rule.EmitLiteral(value).Should().EndWith("m");
		rule.EmitDefault().Should().Be("0m");
	}
}
