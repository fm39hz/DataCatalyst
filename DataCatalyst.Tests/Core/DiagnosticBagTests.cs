namespace DataCatalyst.Tests.Core;

using Xunit;

public class DiagnosticBagTests {
	[Fact]
	public void Info_Warn_Error_RespectSeverity() {
		var bag = new DiagnosticBag();
		bag.Info("info msg");
		bag.Warn("warn msg");

		Assert.Equal(2, bag.Items.Count);
		Assert.False(bag.HasErrors);

		Assert.Equal(Severity.Info, bag.Items[0].Severity);
		Assert.Equal(Severity.Warning, bag.Items[1].Severity);

		bag.Error("error msg");
		Assert.Equal(3, bag.Items.Count);
		Assert.True(bag.HasErrors);
		Assert.Equal(Severity.Error, bag.Items[2].Severity);
	}

	[Fact]
	public void HasErrors_TrueAfterAnyError() {
		var bag = new DiagnosticBag();
		bag.Error("fail");
		Assert.True(bag.HasErrors);
	}

	[Fact]
	public void HasErrors_FalseWithOnlyInfo() {
		var bag = new DiagnosticBag();
		bag.Info("note");
		Assert.False(bag.HasErrors);
	}

	[Fact]
	public void DiagnosticItem_ToString_IncludesSeverity() {
		var item = new DiagnosticItem("test", Severity.Warning);
		var s = item.ToString();
		Assert.Contains("Warning", s);
		Assert.Contains("test", s);
	}
}
