using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.Reflection;

namespace ProjGraph.Tests.Contract;

public class McpContractTests
{
    [Fact]
    public void ProjGraphTools_ShouldHave_McpServerToolTypeAttribute()
    {
        // Arrange
        var type = typeof(ProjGraphTools);

        // Assert class attribute exists
        var classAttr = type.GetCustomAttribute<McpServerToolTypeAttribute>();
        classAttr.Should().NotBeNull("ProjGraphTools class should be marked with McpServerToolType attribute");
    }
}
