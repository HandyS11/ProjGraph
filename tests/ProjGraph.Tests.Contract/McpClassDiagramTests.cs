using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Lib.ClassDiagram.Application;
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
        var method = type.GetMethod("GetClassDiagramAsync");

        // Assert method exists
        method.Should().NotBeNull("GetClassDiagramAsync method should exist");

        // Assert return type is Task<string> (async method)
        method.ReturnType.Should().Be<Task<string>>("GetClassDiagram should return Task<string> (async)");

        // Assert method has McpServerTool attribute
        var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
        toolAttr.Should().NotBeNull("GetClassDiagram should have McpServerTool attribute");

        // Assert method has Description attribute
        var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
        descAttr.Should().NotBeNull();
        descAttr.Description.Should().Contain("directory");
    }

    [Fact]
    public void GetClassDiagram_ShouldHave_RequiredParameters()
    {
        // Arrange
        var method = typeof(ProjGraphTools).GetMethod("GetClassDiagramAsync");
        var parameters = method!.GetParameters();

        // Check filePath parameter
        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Which;
        pathParam.ParameterType.Should().Be<string>();
        pathParam.GetCustomAttribute<DescriptionAttribute>().Should().NotBeNull();

        // Check options parameter
        var optionsParam = parameters.Should().ContainSingle(p => p.Name == "options").Which;
        optionsParam.ParameterType.Should().Be<AnalysisOptions>();
        optionsParam.IsOptional.Should().BeTrue();
        optionsParam.DefaultValue.Should().BeNull();
        optionsParam.GetCustomAttribute<DescriptionAttribute>().Should().NotBeNull();

        // Check showTitle parameter
        var titleParam = parameters.Should().ContainSingle(p => p.Name == "showTitle").Which;
        titleParam.ParameterType.Should().Be<bool>();
        titleParam.IsOptional.Should().BeTrue();
        titleParam.DefaultValue.Should().Be(true);
        titleParam.GetCustomAttribute<DescriptionAttribute>().Should().NotBeNull();

        // Check cancellationToken parameter
        var ctParam = parameters.Should().ContainSingle(p => p.Name == "cancellationToken").Which;
        ctParam.ParameterType.Should().Be<CancellationToken>();
        ctParam.IsOptional.Should().BeTrue();
    }
}
