using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.ComponentModel;
using System.Reflection;

namespace ProjGraph.Tests.Contract;

public class McpProjectGraphContractTests
{
    [Fact]
    public void GetProjectGraph_ShouldHave_CorrectSignature()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetProjectGraphAsync");

        // Assert method exists
        method.Should().NotBeNull("GetProjectGraphAsync method should exist");

        // Assert return type is Task<string>
        method.ReturnType.Should().Be<Task<string>>("GetProjectGraph should return a Task<string>");

        // Assert method has McpServerTool attribute
        var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
        toolAttr.Should().NotBeNull("GetProjectGraph should have McpServerTool attribute");

        // Assert method has Description attribute with meaningful content
        var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
        descAttr.Should().NotBeNull("GetProjectGraph should have Description attribute");
        descAttr.Description.Should().NotBeNullOrWhiteSpace()
            .And.Contain("dependency graph", "description should mention dependency graph");
    }

    [Fact]
    public void GetProjectGraph_ShouldHave_PathParameter()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetProjectGraphAsync");
        var parameters = method!.GetParameters();

        // Assert 'path' parameter exists with correct type
        var pathParam = parameters.Should().Contain(p => p.Name == "path").Subject;
        pathParam.ParameterType.Should().Be<string>("path parameter should be a string");

        // Assert 'path' parameter has Description attribute
        var paramDescAttr = pathParam.GetCustomAttribute<DescriptionAttribute>();
        paramDescAttr.Should().NotBeNull("path parameter should have Description attribute");
        paramDescAttr.Description.Should().NotBeNullOrWhiteSpace()
            .And.Contain("path", "parameter description should mention path");
    }

    [Fact]
    public void GetProjectGraph_ShouldHave_Parameters()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetProjectGraphAsync");
        var parameters = method!.GetParameters();

        // Assert parameters exist (path, showTitle, cancellationToken)
        parameters.Should().HaveCount(3,
            "GetProjectGraph should have 'path', 'showTitle', and 'cancellationToken' parameters");

        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Which;
        pathParam.ParameterType.Should().Be<string>();
        pathParam.IsOptional.Should().BeFalse();

        var titleParam = parameters.Should().ContainSingle(p => p.Name == "showTitle").Which;
        titleParam.ParameterType.Should().Be<bool>();
        titleParam.IsOptional.Should().BeTrue();
        titleParam.DefaultValue.Should().Be(true);

        var ctParam = parameters.Should().ContainSingle(p => p.Name == "cancellationToken").Which;
        ctParam.ParameterType.Should().Be<CancellationToken>();
        ctParam.IsOptional.Should().BeTrue();
    }
}
