using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.ComponentModel;
using System.Reflection;

namespace ProjGraph.Tests.Contract;

public class McpClassDiagramTests
{
    [Fact]
    public void GetClassDiagram_ShouldHave_CorrectSignature()
    {
        // Arrange
        var type = typeof(ProjGraphTools);
        var method = type.GetMethod("GetClassDiagram");

        // Assert method exists
        method.Should().NotBeNull("GetClassDiagram method should exist");

        // Assert return type is Task<string> (async method)
        method.ReturnType.Should().Be<Task<string>>("GetClassDiagram should return Task<string> (async)");

        // Assert method has McpServerTool attribute
        var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
        toolAttr.Should().NotBeNull("GetClassDiagram should have McpServerTool attribute");

        // Assert method has Description attribute
        var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
        descAttr.Should().NotBeNull();
        descAttr!.Description.Should().Contain("class diagram");
    }

    [Fact]
    public void GetClassDiagram_ShouldHave_RequiredParameters()
    {
        // Arrange
        var method = typeof(ProjGraphTools).GetMethod("GetClassDiagram");
        var parameters = method!.GetParameters();

        // Check filePath parameter
        var pathParam = parameters.Should().ContainSingle(p => p.Name == "filePath").Which;
        pathParam.ParameterType.Should().Be<string>();
        pathParam.GetCustomAttribute<DescriptionAttribute>().Should().NotBeNull();

        // Check optional flags
        parameters.Should().ContainSingle(p => p.Name == "includeInheritance");
        parameters.Should().ContainSingle(p => p.Name == "includeDependencies");
        parameters.Should().ContainSingle(p => p.Name == "depth");
    }
}


