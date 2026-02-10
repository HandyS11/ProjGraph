using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.ComponentModel;
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

    [Fact]
    public void ProjGraphTools_ShouldHave_ExpectedToolCount()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        // Assert number of tools (methods with McpServerTool attribute)
        var tools = methods.Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null).ToList();
        tools.Should().HaveCount(3,
            "ProjGraphTools should define exactly 3 tools: GetProjectGraph, GetClassDiagram, and GetErd");
    }

    [Fact]
    public void GetProjectGraph_ShouldHave_CorrectSignature()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetProjectGraph");

        // Assert method exists
        method.Should().NotBeNull("GetProjectGraph method should exist");

        // Assert return type is string
        method.ReturnType.Should().Be<string>("GetProjectGraph should return a string");

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
        var method = type.GetMethod("GetProjectGraph");
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
        var method = type.GetMethod("GetProjectGraph");
        var parameters = method!.GetParameters();

        // Assert parameters exist
        parameters.Should().HaveCount(2, "GetProjectGraph should have 'path' and 'includeTitle' parameters");

        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Which;
        pathParam.ParameterType.Should().Be<string>();
        pathParam.IsOptional.Should().BeFalse();

        var titleParam = parameters.Should().ContainSingle(p => p.Name == "includeTitle").Which;
        titleParam.ParameterType.Should().Be<bool>();
        titleParam.IsOptional.Should().BeTrue();
        titleParam.DefaultValue.Should().Be(true);
    }
}