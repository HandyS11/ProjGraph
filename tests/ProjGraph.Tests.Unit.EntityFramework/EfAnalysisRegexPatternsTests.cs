using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="EfAnalysisRegexPatterns"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EfAnalysisRegexPatternsTests
{
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
