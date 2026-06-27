namespace DataCatalyst.Tests.Core;

using DataCatalyst.Compare;
using Xunit;

public class OperatorParserTests {
	[Theory]
	[InlineData("==", CompareOp.Equal)]
	[InlineData("=", CompareOp.Equal)]
	[InlineData("!=", CompareOp.NotEqual)]
	[InlineData("<>", CompareOp.NotEqual)]
	[InlineData("<", CompareOp.LessThan)]
	[InlineData("<=", CompareOp.LessThanOrEqual)]
	[InlineData(">", CompareOp.GreaterThan)]
	[InlineData(">=", CompareOp.GreaterThanOrEqual)]
	public void Parse_ValidOperators(string input, CompareOp expected) => Assert.Equal(expected, OperatorParser.Parse(input));

	[Fact]
	public void Parse_InvalidOperator_Throws() => Assert.Throws<ArgumentException>(() => OperatorParser.Parse("=?"));

	[Theory]
	[InlineData("==", true)]
	[InlineData("?!?", false)]
	[InlineData("", false)]
	public void TryParse(string input, bool expected) => Assert.Equal(expected, OperatorParser.TryParse(input, out var _));

	[Theory]
	[InlineData(5f, CompareOp.Equal, 5f, true)]
	[InlineData(5f, CompareOp.Equal, 6f, false)]
	[InlineData(3f, CompareOp.LessThan, 5f, true)]
	[InlineData(5f, CompareOp.LessThan, 5f, false)]
	[InlineData(5f, CompareOp.LessThanOrEqual, 5f, true)]
	[InlineData(7f, CompareOp.GreaterThan, 5f, true)]
	[InlineData(5f, CompareOp.GreaterThan, 5f, false)]
	[InlineData(4f, CompareOp.NotEqual, 5f, true)]
	public void Evaluate(float value, CompareOp op, float threshold, bool expected) => Assert.Equal(expected, OperatorParser.Evaluate(value, op, threshold));

	[Fact]
	public void Evaluate_InvalidOp_Throws() => Assert.Throws<ArgumentOutOfRangeException>(
			() => OperatorParser.Evaluate(0f, (CompareOp)99, 0f));
}
