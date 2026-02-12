using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="FluentApiParsingUtilities"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FluentApiParsingUtilitiesTests
{
    [Theory]
    [InlineData("Property<string>", "string")]
    [InlineData("HasForeignKey<Order>", "Order")]
    [InlineData("HasOne<Customer>", "Customer")]
    public void ExtractGenericType_WithGenericType_ShouldExtractType(string methodName, string expected)
    {
        FluentApiParsingUtilities.ExtractGenericType(methodName).Should().Be(expected);
    }

    [Theory]
    [InlineData("Property")]
    [InlineData("HasKey")]
    [InlineData("IsRequired")]
    public void ExtractGenericType_NoGenericType_ShouldReturnEmpty(string methodName)
    {
        FluentApiParsingUtilities.ExtractGenericType(methodName).Should().BeEmpty();
    }

    [Fact]
    public void ExtractPropertyNamesFromArgs_LambdaExpression_ShouldExtractPropertyName()
    {
        const string args = "e => e.Name";
        // Note: this uses MethodChainRegex which captures ".Name"
        var result = FluentApiParsingUtilities.ExtractPropertyNamesFromArgs(args);

        result.Should().Contain("Name");
    }

    [Fact]
    public void ExtractPropertyNamesFromArgs_StringLiterals_ShouldExtractNames()
    {
        const string args = "\"Prop1\", \"Prop2\"";

        var result = FluentApiParsingUtilities.ExtractPropertyNamesFromArgs(args);

        result.Should().BeEquivalentTo("Prop1", "Prop2");
    }

    [Fact]
    public void ExtractPropertyNamesFromArgs_EmptyArgs_ShouldReturnEmpty()
    {
        var result = FluentApiParsingUtilities.ExtractPropertyNamesFromArgs("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractTargetName_StringLiteral_ShouldExtractName()
    {
        const string args = "\"Customer\"";

        var result = FluentApiParsingUtilities.ExtractTargetName(args);

        result.Should().Be("Customer");
    }

    [Fact]
    public void ExtractTargetName_NamespaceQualifiedString_ShouldExtractLastPart()
    {
        const string args = "\"MyApp.Models.Customer\"";

        var result = FluentApiParsingUtilities.ExtractTargetName(args);

        result.Should().Be("Customer");
    }

    [Fact]
    public void ExtractTargetName_LambdaExpression_ShouldExtractPropertyName()
    {
        const string args = "e => e.Customer";

        var result = FluentApiParsingUtilities.ExtractTargetName(args);

        result.Should().Be("Customer");
    }

    [Fact]
    public void ExtractTargetName_NoMatchablePattern_ShouldReturnNull()
    {
        var result = FluentApiParsingUtilities.ExtractTargetName("");

        result.Should().BeNull();
    }

    [Fact]
    public void GetOrCreateProperty_ExistingProperty_ShouldReturnExisting()
    {
        var entity = new EfEntity
        {
            Name = "Order"
        };
        entity.Properties.Add(new EfProperty
        {
            Name = "Total",
            Type = "decimal"
        });

        var result = FluentApiParsingUtilities.GetOrCreateProperty(entity, "Total", "");

        result.Name.Should().Be("Total");
        result.Type.Should().Be("decimal");
        entity.Properties.Should().HaveCount(1);
    }

    [Fact]
    public void GetOrCreateProperty_NewProperty_ShouldCreateAndAdd()
    {
        var entity = new EfEntity
        {
            Name = "Order"
        };

        var result = FluentApiParsingUtilities.GetOrCreateProperty(entity, "NewProp", "int");

        result.Name.Should().Be("NewProp");
        result.Type.Should().Be("int");
        entity.Properties.Should().HaveCount(1);
    }

    [Fact]
    public void GetOrCreateProperty_NewPropertyWithIdSuffix_ShouldInferGuidType()
    {
        var entity = new EfEntity
        {
            Name = "Order"
        };

        var result = FluentApiParsingUtilities.GetOrCreateProperty(entity, "CustomerId", "");

        result.Type.Should().Be("Guid");
    }

    [Fact]
    public void GetOrCreateProperty_NewPropertyWithoutSuffix_ShouldInferStringType()
    {
        var entity = new EfEntity
        {
            Name = "Order"
        };

        var result = FluentApiParsingUtilities.GetOrCreateProperty(entity, "Description", "");

        result.Type.Should().Be("string");
    }

    [Fact]
    public void IsValueTypeString_IntType_ShouldReturnTrue()
    {
        FluentApiParsingUtilities.IsValueTypeString("int").Should().BeTrue();
    }

    [Fact]
    public void IsValueTypeString_StringType_ShouldReturnFalse()
    {
        FluentApiParsingUtilities.IsValueTypeString("string").Should().BeFalse();
    }

    [Theory]
    [InlineData("Guid", true)]
    [InlineData("DateTime", true)]
    [InlineData("decimal", true)]
    [InlineData("bool", true)]
    [InlineData("MyCustomClass", false)]
    public void IsValueTypeString_VariousTypes_ShouldReturnExpected(string type, bool expected)
    {
        FluentApiParsingUtilities.IsValueTypeString(type).Should().Be(expected);
    }

    [Fact]
    public void IsInsideUsingEntityBlock_InsideBlock_ShouldReturnTrue()
    {
        const string configSection = ".HasMany().WithMany().UsingEntity(j => { j.HasKey(x => x.Id); })";
        var matchIndex = configSection.IndexOf("HasKey", StringComparison.Ordinal);

        FluentApiParsingUtilities.IsInsideUsingEntityBlock(configSection, matchIndex).Should().BeTrue();
    }

    [Fact]
    public void IsInsideUsingEntityBlock_OutsideBlock_ShouldReturnFalse()
    {
        const string configSection = ".HasOne().WithMany()";
        var matchIndex = configSection.IndexOf("WithMany", StringComparison.Ordinal);

        FluentApiParsingUtilities.IsInsideUsingEntityBlock(configSection, matchIndex).Should().BeFalse();
    }
}
