using FluentAssertions;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.Reflection;

namespace ProjGraph.Tests.Contract;

public class McpResourceContractTests
{
    [Fact]
    public void ProjGraphResources_ShouldHave_McpServerResourceTypeAttribute()
    {
        var type = typeof(ProjGraphResources);
        var attr = type.GetCustomAttribute<McpServerResourceTypeAttribute>();
        attr.Should().NotBeNull("ProjGraphResources class should be marked with McpServerResourceType attribute");
    }

    [Fact]
    public void GetWelcome_ShouldHave_McpServerResourceAttribute()
    {
        var method = typeof(ProjGraphResources).GetMethod("GetWelcome");
        method.Should().NotBeNull();

        var attr = method.GetCustomAttribute<McpServerResourceAttribute>();
        attr.Should().NotBeNull("GetWelcome must have [McpServerResource] attribute");
    }

    [Fact]
    public void GetWelcome_ShouldHave_CorrectName()
    {
        var method = typeof(ProjGraphResources).GetMethod("GetWelcome");
        var attr = method!.GetCustomAttribute<McpServerResourceAttribute>()!;
        attr.Name.Should().Be("projgraph-welcome");
    }

    [Fact]
    public void GetWelcome_ShouldHave_CorrectMimeType()
    {
        var method = typeof(ProjGraphResources).GetMethod("GetWelcome");
        var attr = method!.GetCustomAttribute<McpServerResourceAttribute>()!;
        attr.MimeType.Should().Be("text/plain");
    }

    [Fact]
    public void GetWelcome_ShouldHave_WelcomeUriTemplate()
    {
        var method = typeof(ProjGraphResources).GetMethod("GetWelcome");
        var attr = method!.GetCustomAttribute<McpServerResourceAttribute>()!;
        attr.UriTemplate.Should().Be("projgraph://welcome");
    }

    [Fact]
    public void GetWelcome_ShouldReturn_String()
    {
        var method = typeof(ProjGraphResources).GetMethod("GetWelcome");
        method.Should().NotBeNull();
        method.ReturnType.Should().Be<string>();
    }

    [Fact]
    public void GetWelcome_ShouldBe_Static()
    {
        var method = typeof(ProjGraphResources).GetMethod("GetWelcome");
        method.Should().NotBeNull();
        method.IsStatic.Should().BeTrue("GetWelcome does not require DI and should be static");
    }

    [Fact]
    public void ReadDiagram_ShouldHave_McpServerResourceAttribute()
    {
        var method = typeof(ProjGraphResources).GetMethod("ReadDiagram");
        method.Should().NotBeNull();

        var attr = method.GetCustomAttribute<McpServerResourceAttribute>();
        attr.Should().NotBeNull("ReadDiagram must have [McpServerResource] attribute");
    }

    [Fact]
    public void ReadDiagram_ShouldHave_DiagramUriTemplate()
    {
        var method = typeof(ProjGraphResources).GetMethod("ReadDiagram");
        var attr = method!.GetCustomAttribute<McpServerResourceAttribute>()!;
        attr.UriTemplate.Should().Be("projgraph://diagrams/{type}/{path}");
    }

    [Fact]
    public void ReadDiagram_ShouldHave_CorrectName()
    {
        var method = typeof(ProjGraphResources).GetMethod("ReadDiagram");
        var attr = method!.GetCustomAttribute<McpServerResourceAttribute>()!;
        attr.Name.Should().Be("projgraph-diagram");
    }

    [Fact]
    public void ReadDiagram_ShouldHave_CorrectParameters()
    {
        var method = typeof(ProjGraphResources).GetMethod("ReadDiagram");
        method.Should().NotBeNull();

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(2);

        parameters[0].Name.Should().Be("type");
        parameters[0].ParameterType.Should().Be<string>();

        parameters[1].Name.Should().Be("path");
        parameters[1].ParameterType.Should().Be<string>();
    }

    [Fact]
    public void DiagramResourceCache_MaxCachedResources_ShouldBe50()
    {
        DiagramResourceCache.MaxCachedResources.Should().Be(50);
    }

    [Fact]
    public void WelcomeContent_ShouldContain_AllToolNames()
    {
        var content = ProjGraphResources.GetWelcome();
        content.Should().Contain("get_project_graph");
        content.Should().Contain("get_class_diagram");
        content.Should().Contain("get_erd");
        content.Should().Contain("get_project_stats");
    }

    [Fact]
    public void WelcomeContent_ShouldContain_AllPromptNames()
    {
        var content = ProjGraphResources.GetWelcome();
        content.Should().Contain("architecture_review");
        content.Should().Contain("dependency_analysis");
        content.Should().Contain("database_schema_review");
        content.Should().Contain("class_structure_review");
    }

    [Fact]
    public void WelcomeContent_ShouldContain_DiagramCacheInfo()
    {
        var content = ProjGraphResources.GetWelcome();
        content.Should().Contain("projgraph://diagrams/{type}/{encodedPath}");
    }
}
