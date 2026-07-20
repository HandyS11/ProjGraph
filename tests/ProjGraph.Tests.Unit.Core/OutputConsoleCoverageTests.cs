using ProjGraph.Lib.Core.Infrastructure;
using Spectre.Console;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Coverage for <see cref="SpectreOutputConsole"/> write paths and status handling.
/// Diagnostic messages (info/error/warning/success) must go to standard error so that piped
/// standard output stays machine-readable, and markup metacharacters in user-supplied text must
/// be escaped rather than interpreted.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutputConsoleCoverageTests
{
    private readonly SpectreOutputConsole _sut = new();

    /// <summary>
    /// Runs an action with <see cref="AnsiConsole.Console"/> redirected to an in-memory,
    /// colour-free console and returns everything written to it.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <returns>The captured standard-output text.</returns>
    private static string CaptureStandardOutput(Action action)
    {
        var original = AnsiConsole.Console;
        var writer = new StringWriter();
        try
        {
            AnsiConsole.Console = CreatePlainConsole(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            AnsiConsole.Console = original;
        }
    }

    /// <summary>
    /// Runs an action with <see cref="Console.Error"/> redirected and returns everything written
    /// to it. <see cref="AnsiConsole.Console"/> is redirected too so no output escapes to the real
    /// terminal and the stderr console inherits colour-free capabilities.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <returns>The captured standard-error text.</returns>
    private static string CaptureStandardError(Action action)
    {
        var originalError = Console.Error;
        var originalConsole = AnsiConsole.Console;
        var errorWriter = new StringWriter();
        var outWriter = new StringWriter();
        try
        {
            Console.SetError(errorWriter);
            AnsiConsole.Console = CreatePlainConsole(outWriter);
            action();
            return errorWriter.ToString();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
            Console.SetError(originalError);
        }
    }

    private static IAnsiConsole CreatePlainConsole(TextWriter writer)
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
    }

    [Fact]
    public void Write_ShouldWriteMessageToStandardOutputWithoutNewline()
    {
        var output = CaptureStandardOutput(() => _sut.Write("payload"));

        output.Should().Be("payload");
    }

    [Fact]
    public void WriteLine_ShouldWriteMessageToStandardOutputWithNewline()
    {
        var output = CaptureStandardOutput(() => _sut.WriteLine("payload"));

        output.Should().Contain("payload");
        output.Should().EndWith(Environment.NewLine);
    }

    [Fact]
    public void WriteMarkup_ShouldInterpretMarkupTags()
    {
        // WriteMarkup is the explicit opt-in for pre-formatted text: the tags must be consumed as
        // styling rather than emitted literally.
        var output = CaptureStandardOutput(() => _sut.WriteMarkup("[bold]styled[/]"));

        output.Should().Contain("styled");
        output.Should().NotContain("[bold]");
    }

    [Fact]
    public void WriteInfo_ShouldWriteToStandardErrorNotStandardOutput()
    {
        var error = CaptureStandardError(() => _sut.WriteInfo("informational"));

        error.Should().Contain("informational");
    }

    [Fact]
    public void WriteError_ShouldWriteToStandardErrorWithErrorPrefix()
    {
        var error = CaptureStandardError(() => _sut.WriteError("boom"));

        error.Should().Contain("Error: boom");
    }

    [Fact]
    public void WriteWarning_ShouldWriteToStandardErrorWithWarningPrefix()
    {
        var error = CaptureStandardError(() => _sut.WriteWarning("careful"));

        error.Should().Contain("Warning: careful");
    }

    [Fact]
    public void WriteSuccess_ShouldWriteToStandardError()
    {
        var error = CaptureStandardError(() => _sut.WriteSuccess("all good"));

        error.Should().Contain("all good");
    }

    [Theory]
    [InlineData("[not markup]")]
    [InlineData("value [/] end")]
    public void WriteError_MessageContainingMarkupMetacharacters_ShouldBeEscapedNotInterpreted(string message)
    {
        // Failure messages routinely contain paths and generic type names with square brackets.
        // They must survive verbatim instead of being parsed as markup (or throwing).
        var error = CaptureStandardError(() => _sut.WriteError(message));

        error.Should().Contain(message);
    }

    [Fact]
    public void WriteWarning_MessageContainingMarkupMetacharacters_ShouldBeEscapedNotInterpreted()
    {
        const string message = "skipped Foo[T].csproj";

        var error = CaptureStandardError(() => _sut.WriteWarning(message));

        error.Should().Contain(message);
    }

    [Fact]
    public void WriteSuccess_MessageContainingMarkupMetacharacters_ShouldBeEscapedNotInterpreted()
    {
        const string message = "wrote [output].md";

        var error = CaptureStandardError(() => _sut.WriteSuccess(message));

        error.Should().Contain(message);
    }

    [Fact]
    public async Task RunWithStatusAsync_ShouldExecuteAction()
    {
        var executed = false;
        var original = AnsiConsole.Console;
        var writer = new StringWriter();
        try
        {
            AnsiConsole.Console = CreatePlainConsole(writer);

            await _sut.RunWithStatusAsync("Working...", () =>
            {
                executed = true;
                return Task.CompletedTask;
            });
        }
        finally
        {
            AnsiConsole.Console = original;
        }

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task RunWithStatusAsync_AlreadyCancelledToken_ShouldThrowWithoutRunningAction()
    {
        var executed = false;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _sut.RunWithStatusAsync("Working...", () =>
        {
            executed = true;
            return Task.CompletedTask;
        }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task RunWithStatusAsync_ActionThrows_ShouldPropagateException()
    {
        var original = AnsiConsole.Console;
        var writer = new StringWriter();
        try
        {
            AnsiConsole.Console = CreatePlainConsole(writer);

            var act = () => _sut.RunWithStatusAsync("Working...",
                () => throw new InvalidOperationException("inner failure"));

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("inner failure");
        }
        finally
        {
            AnsiConsole.Console = original;
        }
    }
}
