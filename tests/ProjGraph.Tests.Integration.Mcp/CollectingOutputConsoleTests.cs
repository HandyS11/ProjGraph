using ProjGraph.Mcp;

namespace ProjGraph.Tests.Integration.Mcp;

/// <summary>
/// Covers <see cref="CollectingOutputConsole"/>, the MCP server's stdout-safe console. Every write
/// is discarded — stdout is reserved for the JSON-RPC transport — except warnings, which are
/// buffered per async flow so a tool can surface them in its own result.
/// </summary>
public sealed class CollectingOutputConsoleTests
{
    [Fact]
    public void DrainWarnings_WithoutAnActiveScope_ShouldReturnEmpty()
    {
        var console = new CollectingOutputConsole();

        console.WriteWarning("emitted outside a tool invocation");

        // No collection scope was opened, so the warning is dropped instead of leaking into the
        // result of whichever tool happens to drain next.
        console.DrainWarnings().Should().BeEmpty();
    }

    [Fact]
    public void DrainWarnings_AfterClearWarnings_ShouldReturnWarningsInOrder()
    {
        var console = new CollectingOutputConsole();
        console.ClearWarnings();

        console.WriteWarning("skipped Foo.csproj");
        console.WriteWarning("skipped Bar.csproj");

        console.DrainWarnings().Should().Equal("skipped Foo.csproj", "skipped Bar.csproj");
    }

    [Fact]
    public void DrainWarnings_CalledTwice_ShouldReturnEmptyOnTheSecondCall()
    {
        var console = new CollectingOutputConsole();
        console.ClearWarnings();
        console.WriteWarning("partial analysis");

        console.DrainWarnings().Should().ContainSingle();

        // Draining ends the collection, so the same warning is not reported by the next tool call.
        console.DrainWarnings().Should().BeEmpty();
    }

    [Fact]
    public void DrainWarnings_ShouldReturnASnapshotDetachedFromTheBuffer()
    {
        var console = new CollectingOutputConsole();
        console.ClearWarnings();
        console.WriteWarning("first");

        var drained = console.DrainWarnings();

        console.ClearWarnings();
        console.WriteWarning("second");

        // An already-returned result is a snapshot: warnings collected afterwards must not appear
        // in it, or a tool's result could be mutated after it was built.
        drained.Should().Equal("first");
    }

    [Fact]
    public void ClearWarnings_ShouldDiscardWarningsFromThePreviousScope()
    {
        var console = new CollectingOutputConsole();
        console.ClearWarnings();
        console.WriteWarning("stale");

        console.ClearWarnings();
        console.WriteWarning("fresh");

        console.DrainWarnings().Should().Equal("fresh");
    }

    [Fact]
    public void NonWarningWrites_ShouldNotBeCollected()
    {
        var console = new CollectingOutputConsole();
        console.ClearWarnings();

        console.Write("write");
        console.WriteLine("write line");
        console.WriteInfo("info");
        console.WriteError("error");
        console.WriteSuccess("success");
        console.WriteMarkup("[red]markup[/]");

        // Only warnings are surfaced to the client; every other channel is discarded so nothing
        // (least of all Spectre markup) can reach the JSON-RPC stdio transport.
        console.DrainWarnings().Should().BeEmpty();
    }

    [Fact]
    public async Task WriteWarning_ConcurrentFlows_ShouldNotSeeEachOthersWarnings()
    {
        var console = new CollectingOutputConsole();

        var first = Task.Run(async () =>
        {
            console.ClearWarnings();
            await Task.Yield();
            console.WriteWarning("from-first");
            return console.DrainWarnings();
        });

        var second = Task.Run(async () =>
        {
            console.ClearWarnings();
            await Task.Yield();
            console.WriteWarning("from-second");
            return console.DrainWarnings();
        });

        var results = await Task.WhenAll(first, second);

        // The buffer lives in an AsyncLocal, so concurrent tool invocations stay isolated even
        // though the console is registered as a singleton.
        results[0].Should().Equal("from-first");
        results[1].Should().Equal("from-second");
    }

    [Fact]
    public async Task PromptSelectionAsync_WithChoices_ShouldReturnTheFirstChoice()
    {
        var console = new CollectingOutputConsole();

        var selection = await console.PromptSelectionAsync("Select a DbContext", ["Alpha", "Beta"]);

        // The MCP server is non-interactive, so a prompt resolves to the first choice rather than
        // blocking forever waiting for input that can never arrive.
        selection.Should().Be("Alpha");
    }

    [Fact]
    public async Task PromptSelectionAsync_WithNoChoices_ShouldThrowNamingThePrompt()
    {
        var console = new CollectingOutputConsole();

        var act = async () => await console.PromptSelectionAsync("Select a DbContext", []);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Select a DbContext*");
    }

    [Fact]
    public async Task RunWithStatusAsync_ShouldInvokeTheAction()
    {
        var console = new CollectingOutputConsole();
        var invoked = false;

        await console.RunWithStatusAsync("Analyzing", () =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task RunWithStatusAsync_ShouldPropagateTheActionFailure()
    {
        var console = new CollectingOutputConsole();

        var act = async () => await console.RunWithStatusAsync("Analyzing",
            () => Task.FromException(new InvalidOperationException("boom")));

        // The status wrapper is transparent: it must not swallow the operation's failure.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task RunWithStatusAsync_ShouldCollectWarningsRaisedByTheAction()
    {
        var console = new CollectingOutputConsole();
        console.ClearWarnings();

        await console.RunWithStatusAsync("Analyzing", async () =>
        {
            await Task.Yield();
            console.WriteWarning("skipped a project");
        });

        // The action runs on the caller's async flow, so its warnings land in the caller's scope.
        console.DrainWarnings().Should().Equal("skipped a project");
    }
}
