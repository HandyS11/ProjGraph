using ProjGraph.Lib.Core.Abstractions;
using System.Text;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Tests for <see cref="MermaidFenceHelper"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MermaidFenceHelperTests
{
    [Fact]
    public void AppendFenceStart_NullOptions_ShouldAddMermaidFence()
    {
        var sb = new StringBuilder();

        MermaidFenceHelper.AppendFenceStart(sb, null, null);

        sb.ToString().Should().Contain("```mermaid");
    }

    [Fact]
    public void AppendFenceStart_WrapTrue_ShouldAddMermaidFence()
    {
        var sb = new StringBuilder();
        var options = new DiagramOptions(WrapInMarkdownFence: true);

        MermaidFenceHelper.AppendFenceStart(sb, options, null);

        sb.ToString().Should().StartWith("```mermaid");
    }

    [Fact]
    public void AppendFenceStart_WrapFalse_ShouldNotAddFence()
    {
        var sb = new StringBuilder();
        var options = new DiagramOptions(WrapInMarkdownFence: false);

        MermaidFenceHelper.AppendFenceStart(sb, options, null);

        sb.ToString().Should().NotContain("```mermaid");
    }

    [Fact]
    public void AppendFenceStart_WithTitle_ShouldAddYamlTitleBlock()
    {
        var sb = new StringBuilder();
        var options = new DiagramOptions();

        MermaidFenceHelper.AppendFenceStart(sb, options, "My Diagram");

        var result = sb.ToString();
        result.Should().Contain("---");
        result.Should().Contain("title: My Diagram");
    }

    [Fact]
    public void AppendFenceStart_ShowTitleFalse_ShouldNotAddTitle()
    {
        var sb = new StringBuilder();
        var options = new DiagramOptions(false);

        MermaidFenceHelper.AppendFenceStart(sb, options, "My Diagram");

        sb.ToString().Should().NotContain("title:");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AppendFenceStart_NullOrWhitespaceTitle_ShouldNotAddTitleBlock(string? title)
    {
        var sb = new StringBuilder();
        var options = new DiagramOptions();

        MermaidFenceHelper.AppendFenceStart(sb, options, title);

        sb.ToString().Should().NotContain("title:");
        sb.ToString().Should().NotContain("---");
    }

    [Fact]
    public void AppendFenceEnd_NullOptions_ShouldAddClosingFence()
    {
        var sb = new StringBuilder();

        MermaidFenceHelper.AppendFenceEnd(sb, null);

        sb.ToString().TrimEnd().Should().Be("```");
    }

    [Fact]
    public void AppendFenceEnd_WrapTrue_ShouldAddClosingFence()
    {
        var sb = new StringBuilder();
        var options = new DiagramOptions(WrapInMarkdownFence: true);

        MermaidFenceHelper.AppendFenceEnd(sb, options);

        sb.ToString().TrimEnd().Should().Be("```");
    }

    [Fact]
    public void AppendFenceEnd_WrapFalse_ShouldNotAddClosingFence()
    {
        var sb = new StringBuilder();
        var options = new DiagramOptions(WrapInMarkdownFence: false);

        MermaidFenceHelper.AppendFenceEnd(sb, options);

        sb.ToString().Should().BeEmpty();
    }

    [Fact]
    public void AppendFenceStart_WithTitleAndFence_ShouldProduceCorrectOrder()
    {
        var sb = new StringBuilder();

        MermaidFenceHelper.AppendFenceStart(sb, null, "Test Title");

        var result = sb.ToString();
        var fenceIndex = result.IndexOf("```mermaid", StringComparison.Ordinal);
        var titleIndex = result.IndexOf("title: Test Title", StringComparison.Ordinal);

        fenceIndex.Should().BeLessThan(titleIndex, "fence should come before title");
    }
}
