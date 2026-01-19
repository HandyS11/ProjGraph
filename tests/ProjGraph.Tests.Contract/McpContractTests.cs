using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.ComponentModel;
using System.Reflection;

namespace ProjGraph.Tests.Contract;

public class McpContractTests
{
    [Fact]
    public void ProjGraphTools_ShouldProvide_GetProjectGraph_Tool()
    {
        // Arrange
        var type = typeof(ProjGraphTools);

        // Assert class attribute
        type.GetCustomAttribute<McpServerToolTypeAttribute>().Should().NotBeNull();

        // Assert method exists and has correct attributes
        var method = type.GetMethod("GetProjectGraph");
        method.Should().NotBeNull();
        method.GetCustomAttribute<McpServerToolAttribute>().Should().NotBeNull();
        method.GetCustomAttribute<DescriptionAttribute>().Should().NotBeNull();

        // Assert parameters match spec
        var parameters = method.GetParameters();
        parameters.Should().Contain(p => p.Name == "path" && p.ParameterType == typeof(string));
    }
}
