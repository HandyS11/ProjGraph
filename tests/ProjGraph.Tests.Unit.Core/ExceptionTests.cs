using ProjGraph.Core.Exceptions;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Tests for exception hierarchy: <see cref="ProjGraphException"/>,
/// <see cref="AnalysisException"/>, and <see cref="ParsingException"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExceptionTests
{
    [Fact]
    public void ProjGraphException_DefaultConstructor_ShouldCreate()
    {
        var ex = new ProjGraphException();

        ex.Message.Should().NotBeNullOrEmpty();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void ProjGraphException_WithMessage_ShouldSetMessage()
    {
        var ex = new ProjGraphException("test error");

        ex.Message.Should().Be("test error");
    }

    [Fact]
    public void ProjGraphException_WithInnerException_ShouldSetBoth()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ProjGraphException("outer", inner);

        ex.Message.Should().Be("outer");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void ProjGraphException_ShouldDeriveFromException()
    {
        var ex = new ProjGraphException("test");

        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void AnalysisException_WithMessage_ShouldSetMessage()
    {
        var ex = new AnalysisException("analysis failed");

        ex.Message.Should().Be("analysis failed");
        ex.Should().BeAssignableTo<ProjGraphException>();
    }

    [Fact]
    public void AnalysisException_WithInnerException_ShouldSetBoth()
    {
        var inner = new IOException("io error");
        var ex = new AnalysisException("analysis failed", inner);

        ex.Message.Should().Be("analysis failed");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void ParsingException_WithMessage_ShouldSetMessage()
    {
        var ex = new ParsingException("parse failed");

        ex.Message.Should().Be("parse failed");
        ex.Should().BeAssignableTo<ProjGraphException>();
    }

    [Fact]
    public void ParsingException_WithInnerException_ShouldSetBoth()
    {
        var inner = new FormatException("bad format");
        var ex = new ParsingException("parse failed", inner);

        ex.Message.Should().Be("parse failed");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Theory]
    [InlineData(typeof(ProjGraphException))]
    [InlineData(typeof(AnalysisException))]
    [InlineData(typeof(ParsingException))]
    public void AllExceptions_DefaultConstructor_ShouldCreateInstance(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType)!;

        ex.Should().NotBeNull();
        ex.Should().BeAssignableTo<ProjGraphException>();
    }
}
