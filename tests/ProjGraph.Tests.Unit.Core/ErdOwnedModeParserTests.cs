using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Tests for <see cref="ErdOwnedModeParser"/> — the single helper the CLI (<c>--owned-mode</c>) and MCP
/// (<c>ownedMode</c>) surfaces both call, so their validation and value mapping cannot drift apart.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ErdOwnedModeParserTests
{
    [Theory]
    [InlineData("mirror")]
    [InlineData("MIRROR")]
    [InlineData("Mirror")]
    public void TryParse_Mirror_ReturnsTrueAndMirrorEf(string value)
    {
        var result = ErdOwnedModeParser.TryParse(value, out var mode);

        result.Should().BeTrue();
        mode.Should().Be(ErdOwnedMode.MirrorEf);
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("CLASSIC")]
    [InlineData("Classic")]
    public void TryParse_Classic_ReturnsTrueAndClassic(string value)
    {
        var result = ErdOwnedModeParser.TryParse(value, out var mode);

        result.Should().BeTrue();
        mode.Should().Be(ErdOwnedMode.Classic);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData("mirrorr")]
    [InlineData("mirror ")]
    public void TryParse_Unrecognized_ReturnsFalseAndDefaultsToMirrorEf(string value)
    {
        var result = ErdOwnedModeParser.TryParse(value, out var mode);

        result.Should().BeFalse();
        mode.Should().Be(ErdOwnedMode.MirrorEf);
    }
}
