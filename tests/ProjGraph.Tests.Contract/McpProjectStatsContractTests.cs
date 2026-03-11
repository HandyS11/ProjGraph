using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.ComponentModel;
using System.Reflection;

namespace ProjGraph.Tests.Contract;

public class McpProjectStatsContractTests
{
    [Fact]
    public void GetProjectStats_ShouldHave_CorrectSignature()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetProjectStatsAsync");

        // Assert method exists
        method.Should().NotBeNull("GetProjectStatsAsync method should exist on ProjGraphTools");

        // Assert return type is Task<string>
        method.ReturnType.Should().Be<Task<string>>("GetProjectStats should return a Task<string>");

        // Assert method has McpServerTool attribute with correct tool name
        var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
        toolAttr.Should().NotBeNull("GetProjectStats should have McpServerTool attribute");
        toolAttr.Name.Should().Be("get_project_stats",
            "the MCP tool name must be 'get_project_stats' per spec");

        // Assert method has a meaningful Description
        var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
        descAttr.Should().NotBeNull("GetProjectStats should have a Description attribute");
        descAttr.Description.Should().NotBeNullOrWhiteSpace()
            .And.Contain("metrics", "description should mention 'metrics'");
    }

    [Fact]
    public void GetProjectStats_ShouldHave_RequiredPathParameter()
    {
        // Arrange
        var method = typeof(ProjGraphTools).GetMethod("GetProjectStatsAsync");
        var parameters = method!.GetParameters();

        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Which;
        pathParam.ParameterType.Should().Be<string>("path parameter should be a string");
        pathParam.IsOptional.Should().BeFalse("path must be required");

        var pathDesc = pathParam.GetCustomAttribute<DescriptionAttribute>();
        pathDesc.Should().NotBeNull("path parameter should have Description attribute");
        pathDesc.Description.Should().Contain("path",
            "path parameter description should mention 'path'");
    }

    [Fact]
    public void GetProjectStats_ShouldHave_OptionalTopNParameter()
    {
        // Arrange
        var method = typeof(ProjGraphTools).GetMethod("GetProjectStatsAsync");
        var parameters = method!.GetParameters();

        var topNParam = parameters.Should().ContainSingle(p => p.Name == "topN").Which;
        topNParam.ParameterType.Should().Be<int>("topN parameter should be an int");
        topNParam.IsOptional.Should().BeTrue("topN must be optional");
        topNParam.DefaultValue.Should().Be(5, "topN default value should be 5");

        var topNDesc = topNParam.GetCustomAttribute<DescriptionAttribute>();
        topNDesc.Should().NotBeNull("topN parameter should have Description attribute");
    }

    [Fact]
    public void GetProjectStats_ShouldHave_CancellationTokenParameter()
    {
        // Arrange
        var method = typeof(ProjGraphTools).GetMethod("GetProjectStatsAsync");
        var parameters = method!.GetParameters();

        var ctParam = parameters.Should().ContainSingle(p => p.Name == "cancellationToken").Which;
        ctParam.ParameterType.Should().Be<CancellationToken>("cancellationToken must be CancellationToken");
        ctParam.IsOptional.Should().BeTrue("cancellationToken must be optional");
    }

    [Fact]
    public void GetProjectStats_ShouldHave_CorrectParameterCount()
    {
        // Arrange
        var method = typeof(ProjGraphTools).GetMethod("GetProjectStatsAsync");
        var parameters = method!.GetParameters();

        // path, topN, progress, cancellationToken = 4 parameters
        parameters.Should().HaveCount(4,
            "GetProjectStats should have 'path', 'topN', 'progress', and 'cancellationToken' parameters");
    }
}
