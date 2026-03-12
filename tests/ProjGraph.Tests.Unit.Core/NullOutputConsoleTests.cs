using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Tests for <see cref="NullOutputConsole"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NullOutputConsoleTests
{
    private readonly NullOutputConsole _sut = new();

    [Fact]
    public void Write_ShouldNotThrow()
    {
        var act = () => _sut.Write("test");

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteLine_ShouldNotThrow()
    {
        var act = () => _sut.WriteLine("test");

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteInfo_ShouldNotThrow()
    {
        var act = () => _sut.WriteInfo("test");

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteError_ShouldNotThrow()
    {
        var act = () => _sut.WriteError("test");

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteWarning_ShouldNotThrow()
    {
        var act = () => _sut.WriteWarning("test");

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteSuccess_ShouldNotThrow()
    {
        var act = () => _sut.WriteSuccess("test");

        act.Should().NotThrow();
    }

    [Fact]
    public void WriteMarkup_ShouldNotThrow()
    {
        var act = () => _sut.WriteMarkup("test");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task PromptSelectionAsync_ShouldReturnFirstChoice()
    {
        var choices = new[]
        {
            "option1", "option2", "option3"
        };

        var result = await _sut.PromptSelectionAsync("Pick one", choices);

        result.Should().Be("option1");
    }

    [Fact]
    public async Task PromptSelectionAsync_EmptyChoices_ShouldThrowInvalidOperationException()
    {
        var act = () => _sut.PromptSelectionAsync("Pick one", []);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No choices available*");
    }

    [Fact]
    public async Task RunWithStatusAsync_ShouldExecuteAction()
    {
        var executed = false;

        await _sut.RunWithStatusAsync("Working...", () =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        executed.Should().BeTrue();
    }
}
