using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="EfAnalysisRegexPatterns"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EfAnalysisRegexPatternsTests
{
    [Theory]
    [InlineData("""Entity<Order>("OrderTable")""", "Order")]
    [InlineData("""Entity("Order")""", "Order")]
    public void EntityNameRegex_ValidPatterns_ShouldMatch(string input, string expectedName)
    {
        var match = EfAnalysisRegexPatterns.EntityNameRegex().Match(input);

        match.Success.Should().BeTrue();
        var name = match.Groups[1].Value;
        if (string.IsNullOrEmpty(name))
        {
            name = match.Groups[2].Value;
        }

        name.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(".Entity<Order>", true)]
    [InlineData(""".Entity("Order")""", true)]
    [InlineData(".SomeOtherMethod()", false)]
    public void EntitySplitRegex_ShouldMatchEntityCalls(string input, bool shouldMatch)
    {
        var match = EfAnalysisRegexPatterns.EntitySplitRegex().IsMatch(input);

        match.Should().Be(shouldMatch);
    }

    [Fact]
    public void ShadowRelationshipRegex_HasOneWithMany_ShouldMatch()
    {
        const string input = "HasOne<Customer>().WithMany()";

        var match = EfAnalysisRegexPatterns.ShadowRelationshipRegex().Match(input);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("HasOne");
        match.Groups[2].Value.Should().Be("Customer");
        match.Groups[3].Value.Should().Be("WithMany");
    }

    [Fact]
    public void PropertyLambdaRegex_SimpleLambda_ShouldMatch()
    {
        const string input = "e => e.Name";

        var match = EfAnalysisRegexPatterns.PropertyLambdaRegex().Match(input);

        match.Success.Should().BeTrue();
        match.Groups[2].Value.Should().Be("Name");
    }

    [Fact]
    public void MethodCallRegex_ShouldMatchMethodWithArgs()
    {
        const string input = ".HasMaxLength(100)";

        var match = EfAnalysisRegexPatterns.MethodCallRegex().Match(input);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("HasMaxLength");
        match.Groups[2].Value.Should().Be("100");
    }

    [Fact]
    public void ToTableRegex_ShouldMatchTableName()
    {
        const string input = """.ToTable("Orders")""";

        var match = EfAnalysisRegexPatterns.ToTableRegex().Match(input);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("Orders");
    }

    [Fact]
    public void StringLiteralRegex_ShouldMatchQuotedStrings()
    {
        const string input = """some text "hello" more text""";

        var match = EfAnalysisRegexPatterns.StringLiteralRegex().Match(input);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("hello");
    }

    [Fact]
    public void NumberInParensRegex_ShouldMatchNumericArg()
    {
        const string input = "nvarchar(30)";

        var match = EfAnalysisRegexPatterns.NumberInParensRegex().Match(input);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("30");
    }

    [Fact]
    public void DecimalPrecisionRegex_ShouldMatchDecimalPrecisionScale()
    {
        const string input = "decimal(18, 2)";

        var match = EfAnalysisRegexPatterns.DecimalPrecisionRegex().Match(input);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("18");
        match.Groups[2].Value.Should().Be("2");
    }
}
