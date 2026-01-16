using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.ComponentModel;
using System.Reflection;

namespace ProjGraph.Tests.Contract;

public class McpErdContractTests
{
    [Fact]
    public void ProjGraphTools_ShouldProvide_GetErd_Tool()
    {
        // Arrange
        var type = typeof(ProjGraphTools);

        // Assert method exists and has correct attributes
        var method = type.GetMethod("GetErd");
        method.Should().NotBeNull();
        method.GetCustomAttribute<McpServerToolAttribute>().Should().NotBeNull();
        method.GetCustomAttribute<DescriptionAttribute>().Should().NotBeNull();

        // Assert parameters match spec
        var parameters = method.GetParameters();
        parameters.Should().Contain(p => p.Name == "path" && p.ParameterType == typeof(string));
        parameters.Should().Contain(p => p.Name == "contextName" && p.ParameterType == typeof(string));
    }
}