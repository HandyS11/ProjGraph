using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="EfPropertyFactory"/> and <see cref="EfPropertyOverrides"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EfPropertyFactoryTests
{
    private static EfProperty CreateBaseProperty()
    {
        return new EfProperty
        {
            Name = "TestProp",
            Type = "string",
            IsPrimaryKey = false,
            IsForeignKey = false,
            IsRequired = false,
            IsValueType = false,
            IsExplicitlyRequired = false,
            MaxLength = null,
            Precision = null,
            Scale = null,
            DefaultValue = null
        };
    }

    [Fact]
    public void CopyWith_NoOverrides_ShouldReturnIdenticalCopy()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides());

        result.Name.Should().Be(source.Name);
        result.Type.Should().Be(source.Type);
        result.IsPrimaryKey.Should().Be(source.IsPrimaryKey);
        result.IsForeignKey.Should().Be(source.IsForeignKey);
        result.IsRequired.Should().Be(source.IsRequired);
        result.IsValueType.Should().Be(source.IsValueType);
        result.MaxLength.Should().Be(source.MaxLength);
    }

    [Fact]
    public void CopyWith_OverrideIsPrimaryKey_ShouldOnlyChangeIsPrimaryKey()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides
        {
            IsPrimaryKey = true
        });

        result.IsPrimaryKey.Should().BeTrue();
        result.Name.Should().Be(source.Name);
        result.Type.Should().Be(source.Type);
        result.IsForeignKey.Should().BeFalse();
    }

    [Fact]
    public void CopyWith_OverrideIsForeignKey_ShouldOnlyChangeForeignKey()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides
        {
            IsForeignKey = true
        });

        result.IsForeignKey.Should().BeTrue();
        result.IsPrimaryKey.Should().BeFalse();
    }

    [Fact]
    public void CopyWith_OverrideMaxLength_ShouldSetMaxLength()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides
        {
            MaxLength = 100
        });

        result.MaxLength.Should().Be(100);
    }

    [Fact]
    public void CopyWith_OverridePrecisionAndScale_ShouldSetBoth()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides
        {
            Precision = 18,
            Scale = 2
        });

        result.Precision.Should().Be(18);
        result.Scale.Should().Be(2);
    }

    [Fact]
    public void CopyWith_OverrideDefaultValue_ShouldSetDefaultValue()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides
        {
            DefaultValue = "'N/A'"
        });

        result.DefaultValue.Should().Be("'N/A'");
    }

    [Fact]
    public void CopyWith_OverrideType_ShouldChangeType()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides
        {
            Type = "int"
        });

        result.Type.Should().Be("int");
    }

    [Fact]
    public void CopyWith_OverrideIsRequired_ShouldSetRequired()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides
        {
            IsRequired = true,
            IsExplicitlyRequired = true
        });

        result.IsRequired.Should().BeTrue();
        result.IsExplicitlyRequired.Should().BeTrue();
    }

    [Fact]
    public void CopyWith_OverrideIsValueType_ShouldSetIsValueType()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides
        {
            IsValueType = true
        });

        result.IsValueType.Should().BeTrue();
    }

    [Fact]
    public void CopyWith_MultipleOverrides_ShouldApplyAll()
    {
        var source = CreateBaseProperty();

        var result = EfPropertyFactory.CopyWith(source, new EfPropertyOverrides
        {
            IsPrimaryKey = true,
            IsRequired = true,
            MaxLength = 50,
            Type = "int",
            IsValueType = true
        });

        result.IsPrimaryKey.Should().BeTrue();
        result.IsRequired.Should().BeTrue();
        result.MaxLength.Should().Be(50);
        result.Type.Should().Be("int");
        result.IsValueType.Should().BeTrue();
    }
}
