using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.ComponentModel;
using System.Reflection;

namespace ProjGraph.Tests.Contract;

public class McpErdContractTests
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
    public void GetErd_ShouldHave_CorrectSignature()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetErd");

        // Assert method exists
        method.Should().NotBeNull("GetErd method should exist");

        // Assert return type is Task<string> (async method)
        method.ReturnType.Should().Be<Task<string>>("GetErd should return Task<string> (async)");

        // Assert method has McpServerTool attribute
        var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
        toolAttr.Should().NotBeNull("GetErd should have McpServerTool attribute");

        // Assert method has Description attribute with meaningful content
        var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
        descAttr.Should().NotBeNull("GetErd should have Description attribute");
        descAttr.Description.Should().NotBeNullOrWhiteSpace()
            .And.Contain("ERD", "description should mention ERD")
            .And.Contain("Entity Framework", "description should mention Entity Framework");
    }

    [Fact]
    public void GetErd_ShouldHave_PathParameter()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetErd");
        var parameters = method!.GetParameters();

        // Assert 'path' parameter exists with correct type
        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Subject;
        pathParam.ParameterType.Should().Be<string>("path parameter should be a string");

        // Assert 'path' parameter has Description attribute
        var paramDescAttr = pathParam.GetCustomAttribute<DescriptionAttribute>();
        paramDescAttr.Should().NotBeNull("path parameter should have Description attribute");
        paramDescAttr.Description.Should().NotBeNullOrWhiteSpace()
            .And.Contain("path", "parameter description should mention path");
    }

    [Fact]
    public void GetErd_ShouldHave_OptionalContextNameParameter()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetErd");
        var parameters = method!.GetParameters();

        // Assert 'contextName' parameter exists and is optional (nullable string)
        var contextParam = parameters.Should().ContainSingle(p => p.Name == "contextName").Subject;
        contextParam.ParameterType.Should().Be<string>("contextName parameter should be a string");
        contextParam.IsOptional.Should().BeTrue("contextName parameter should be optional");
        contextParam.HasDefaultValue.Should().BeTrue("contextName should have a default value");
        contextParam.DefaultValue.Should().BeNull("contextName default value should be null");

        // Assert 'contextName' parameter has Description attribute
        var paramDescAttr = contextParam.GetCustomAttribute<DescriptionAttribute>();
        paramDescAttr.Should().NotBeNull("contextName parameter should have Description attribute");
        paramDescAttr.Description.Should().NotBeNullOrWhiteSpace()
            .And.Contain("DbContext", "parameter description should mention DbContext");
    }

    [Fact]
    public void GetErd_ShouldHave_Parameters()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetErd");
        var parameters = method!.GetParameters();

        // Assert parameters exist
        parameters.Should().HaveCount(3, "GetErd should have 3 parameters: path, contextName, and includeTitle");

        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Which;
        pathParam.ParameterType.Should().Be<string>();
        pathParam.IsOptional.Should().BeFalse();

        var contextParam = parameters.Should().ContainSingle(p => p.Name == "contextName").Which;
        contextParam.ParameterType.Should().Be<string>();
        contextParam.IsOptional.Should().BeTrue();
        contextParam.DefaultValue.Should().BeNull();

        var titleParam = parameters.Should().ContainSingle(p => p.Name == "includeTitle").Which;
        titleParam.ParameterType.Should().Be<bool>();
        titleParam.IsOptional.Should().BeTrue();
        titleParam.DefaultValue.Should().Be(true);
    }
}