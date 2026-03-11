using FluentAssertions;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using ProjGraph.Mcp;
using System.Reflection;

namespace ProjGraph.Tests.Contract;

public class McpPromptContractTests
{
    [Fact]
    public void ProjGraphPrompts_ShouldHave_McpServerPromptTypeAttribute()
    {
        var type = typeof(ProjGraphPrompts);
        var attr = type.GetCustomAttribute<McpServerPromptTypeAttribute>();
        attr.Should().NotBeNull("ProjGraphPrompts class should be marked with McpServerPromptType attribute");
    }

    [Theory]
    [InlineData("ArchitectureReview", "architecture_review")]
    [InlineData("DependencyAnalysis", "dependency_analysis")]
    [InlineData("DatabaseSchemaReview", "database_schema_review")]
    [InlineData("ClassStructureReview", "class_structure_review")]
    public void ProjGraphPrompts_ShouldExpose_FourPromptMethods(string methodName, string expectedPromptName)
    {
        var type = typeof(ProjGraphPrompts);
        var method = type.GetMethod(methodName);

        method.Should().NotBeNull($"Prompt method '{methodName}' should exist");

        var promptAttr = method.GetCustomAttribute<McpServerPromptAttribute>();
        promptAttr.Should().NotBeNull($"'{methodName}' must have [McpServerPrompt] attribute");
        promptAttr.Name.Should().Be(expectedPromptName);
    }

    [Theory]
    [InlineData("ArchitectureReview")]
    [InlineData("DependencyAnalysis")]
    [InlineData("DatabaseSchemaReview")]
    [InlineData("ClassStructureReview")]
    public void PromptMethods_ShouldReturn_IEnumerableChatMessage(string methodName)
    {
        var method = typeof(ProjGraphPrompts).GetMethod(methodName);
        method.Should().NotBeNull();
        method.ReturnType.Should().BeAssignableTo<IEnumerable<ChatMessage>>(
            $"{methodName} should return IEnumerable<ChatMessage>");
    }

    [Fact]
    public void ArchitectureReview_ShouldHave_CorrectParameters()
    {
        var method = typeof(ProjGraphPrompts).GetMethod("ArchitectureReview");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);

        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Which;
        pathParam.ParameterType.Should().Be<string>();
        pathParam.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void DependencyAnalysis_ShouldHave_CorrectParameters()
    {
        var method = typeof(ProjGraphPrompts).GetMethod("DependencyAnalysis");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(2);

        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Which;
        pathParam.ParameterType.Should().Be<string>();
        pathParam.IsOptional.Should().BeFalse();

        var topNParam = parameters.Should().ContainSingle(p => p.Name == "topN").Which;
        topNParam.ParameterType.Should().Be<string>();
        topNParam.IsOptional.Should().BeTrue();
        topNParam.DefaultValue.Should().Be("5");
    }

    [Fact]
    public void DatabaseSchemaReview_ShouldHave_CorrectParameters()
    {
        var method = typeof(ProjGraphPrompts).GetMethod("DatabaseSchemaReview");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(2);

        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Which;
        pathParam.ParameterType.Should().Be<string>();
        pathParam.IsOptional.Should().BeFalse();

        var contextParam = parameters.Should().ContainSingle(p => p.Name == "contextName").Which;
        contextParam.ParameterType.Should().Be<string>();
        contextParam.IsOptional.Should().BeTrue();
        contextParam.DefaultValue.Should().BeNull();
    }

    [Fact]
    public void ClassStructureReview_ShouldHave_CorrectParameters()
    {
        var method = typeof(ProjGraphPrompts).GetMethod("ClassStructureReview");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);

        var pathParam = parameters.Should().ContainSingle(p => p.Name == "path").Which;
        pathParam.ParameterType.Should().Be<string>();
        pathParam.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void ArchitectureReview_ShouldReturn_TwoMessages()
    {
        var messages = ProjGraphPrompts.ArchitectureReview("/test/path.slnx").ToList();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(ChatRole.User);
        messages[1].Role.Should().Be(ChatRole.Assistant);
        messages[0].Text.Should().Contain("get_project_graph");
        messages[0].Text.Should().Contain("get_project_stats");
        messages[0].Text.Should().Contain("/test/path.slnx");
    }

    [Fact]
    public void DependencyAnalysis_ShouldReturn_TwoMessages()
    {
        var messages = ProjGraphPrompts.DependencyAnalysis("/test/path.slnx", "10").ToList();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(ChatRole.User);
        messages[1].Role.Should().Be(ChatRole.Assistant);
        messages[0].Text.Should().Contain("get_project_graph");
        messages[0].Text.Should().Contain("get_project_stats");
        messages[0].Text.Should().Contain("topN=10");
        messages[0].Text.Should().Contain("top 10");
    }

    [Fact]
    public void DatabaseSchemaReview_ShouldReturn_TwoMessages_WithoutContextName()
    {
        var messages = ProjGraphPrompts.DatabaseSchemaReview("/test/MyContext.cs").ToList();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(ChatRole.User);
        messages[1].Role.Should().Be(ChatRole.Assistant);
        messages[0].Text.Should().Contain("get_erd");
        messages[0].Text.Should().NotContain("contextName");
    }

    [Fact]
    public void DatabaseSchemaReview_ShouldReturn_TwoMessages_WithContextName()
    {
        var messages = ProjGraphPrompts.DatabaseSchemaReview("/test/MyContext.cs", "AppDbContext").ToList();

        messages.Should().HaveCount(2);
        messages[0].Text.Should().Contain("get_erd");
        messages[0].Text.Should().Contain("contextName=\"AppDbContext\"");
        messages[0].Text.Should().Contain("(using context: AppDbContext)");
    }

    [Fact]
    public void ClassStructureReview_ShouldReturn_TwoMessages()
    {
        var messages = ProjGraphPrompts.ClassStructureReview("/test/MyClass.cs").ToList();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(ChatRole.User);
        messages[1].Role.Should().Be(ChatRole.Assistant);
        messages[0].Text.Should().Contain("get_class_diagram");
        messages[0].Text.Should().Contain("includeInheritance=true");
        messages[0].Text.Should().Contain("includeDependencies=true");
    }
}
