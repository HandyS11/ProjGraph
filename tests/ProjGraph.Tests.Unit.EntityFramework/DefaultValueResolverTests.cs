using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="DefaultValueResolver"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DefaultValueResolverTests
{
    private static readonly EfProperty BaseProperty = new()
    {
        Name = "Status",
        Type = "string",
        IsValueType = false
    };

    [Fact]
    public void CreateWithDefaultValueSql_ShouldTrimQuotesAndSpaces()
    {
        var result = DefaultValueResolver.CreateWithDefaultValueSql(BaseProperty, "\"GETDATE()\"");

        result.DefaultValue.Should().Be("GETDATE()");
        result.Name.Should().Be("Status");
    }

    [Fact]
    public void CreateWithDefaultValueSql_SingleQuotes_ShouldTrim()
    {
        var result = DefaultValueResolver.CreateWithDefaultValueSql(BaseProperty, " 'N/A' ");

        result.DefaultValue.Should().Be("N/A");
    }

    [Fact]
    public void CreateWithDefaultValue_QuotedString_ShouldReturnLiteral()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Dummy { }");

        var result = DefaultValueResolver.CreateWithDefaultValue(BaseProperty, "\"hello\"", compilation);

        result.DefaultValue.Should().Be("hello");
    }

    [Fact]
    public void CreateWithDefaultValue_SingleQuotedString_ShouldReturnLiteral()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Dummy { }");

        var result = DefaultValueResolver.CreateWithDefaultValue(BaseProperty, "'world'", compilation);

        result.DefaultValue.Should().Be("world");
    }

    [Fact]
    public void CreateWithDefaultValue_SimpleLiteral_ShouldReturnAsIs()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Dummy { }");

        var result = DefaultValueResolver.CreateWithDefaultValue(BaseProperty, "42", compilation);

        result.DefaultValue.Should().Be("42");
    }

    [Fact]
    public void CreateWithDefaultValue_DottedEnum_ShouldShortenToLastPart()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Dummy { }");

        var result = DefaultValueResolver.CreateWithDefaultValue(BaseProperty, "MyEnum.Active", compilation);

        result.DefaultValue.Should().Be("Active");
    }

    [Fact]
    public void CreateWithDefaultValue_NumericDotted_ShouldNotShorten()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Dummy { }");

        // Values like "0.7f" should not be shortened
        var result = DefaultValueResolver.CreateWithDefaultValue(BaseProperty, "0.7", compilation);

        result.DefaultValue.Should().Be("0.7");
    }

    [Fact]
    public void CreateWithDefaultValue_ConstField_ShouldResolveValue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Defaults
                                                             {
                                                                 public const string DefaultStatus = "Active";
                                                             }
                                                             """);

        var result = DefaultValueResolver.CreateWithDefaultValue(BaseProperty, "Defaults.DefaultStatus", compilation);

        result.DefaultValue.Should().Be("Active");
    }

    [Fact]
    public void CreateWithDefaultValue_CastExpression_ShouldStripCast()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Dummy { }");

        var result = DefaultValueResolver.CreateWithDefaultValue(BaseProperty, "(int)MyEnum.Value", compilation);

        // After stripping cast, it becomes "MyEnum.Value", then shortened to "Value"
        result.DefaultValue.Should().Be("Value");
    }

    [Fact]
    public void CreateWithDefaultValue_SimpleConstant_ShouldResolve()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Config
                                                             {
                                                                 public const int MaxRetries = 3;
                                                             }
                                                             """);

        var result = DefaultValueResolver.CreateWithDefaultValue(BaseProperty, "MaxRetries", compilation);

        result.DefaultValue.Should().Be("3");
    }
}
