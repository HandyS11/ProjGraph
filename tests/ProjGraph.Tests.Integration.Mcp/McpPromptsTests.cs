using Microsoft.Extensions.AI;
using ProjGraph.Mcp;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpPromptsTests
{
    [Fact]
    public void ArchitectureReview_ShouldReturn_TwoMessages()
    {
        var messages = ProjGraphPrompts.ArchitectureReview(@"D:\Projects\MySolution.slnx").ToList();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(ChatRole.User);
        messages[1].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact]
    public void ArchitectureReview_ShouldReference_CorrectTools()
    {
        var messages = ProjGraphPrompts.ArchitectureReview(@"D:\Projects\MySolution.slnx").ToList();
        var userMessage = messages[0].Text;

        userMessage.Should().Contain("get_project_graph");
        userMessage.Should().Contain("get_project_stats");
        userMessage.Should().Contain(@"D:\Projects\MySolution.slnx");
    }

    [Fact]
    public void DependencyAnalysis_ShouldReturn_TwoMessages()
    {
        var messages = ProjGraphPrompts.DependencyAnalysis(@"D:\Projects\MySolution.slnx").ToList();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(ChatRole.User);
        messages[1].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact]
    public void DependencyAnalysis_ShouldReference_CorrectTools()
    {
        var messages = ProjGraphPrompts.DependencyAnalysis(@"D:\Projects\MySolution.slnx", "10").ToList();
        var userMessage = messages[0].Text;

        userMessage.Should().Contain("get_project_graph");
        userMessage.Should().Contain("get_project_stats");
        userMessage.Should().Contain("10");
    }

    [Fact]
    public void DatabaseSchemaReview_ShouldReturn_TwoMessages()
    {
        var messages = ProjGraphPrompts.DatabaseSchemaReview(@"D:\Projects\MyDbContext.cs").ToList();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(ChatRole.User);
        messages[1].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact]
    public void DatabaseSchemaReview_ShouldReference_CorrectTool()
    {
        var messages = ProjGraphPrompts.DatabaseSchemaReview(@"D:\Projects\MyDbContext.cs", "AppContext").ToList();
        var userMessage = messages[0].Text;

        userMessage.Should().Contain("get_erd");
        userMessage.Should().Contain(@"D:\Projects\MyDbContext.cs");
    }

    [Fact]
    public void ClassStructureReview_ShouldReturn_TwoMessages()
    {
        var messages = ProjGraphPrompts.ClassStructureReview(@"D:\Projects\Models.cs").ToList();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(ChatRole.User);
        messages[1].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact]
    public void ClassStructureReview_ShouldReference_CorrectTool()
    {
        var messages = ProjGraphPrompts.ClassStructureReview(@"D:\Projects\Models.cs").ToList();
        var userMessage = messages[0].Text;

        userMessage.Should().Contain("get_class_diagram");
        userMessage.Should().Contain(@"D:\Projects\Models.cs");
    }

    [Fact]
    public void AllPrompts_ShouldHave_NonEmptyAssistantPreFill()
    {
        var prompts = new List<IEnumerable<ChatMessage>>
        {
            ProjGraphPrompts.ArchitectureReview(@"D:\test.slnx"),
            ProjGraphPrompts.DependencyAnalysis(@"D:\test.slnx"),
            ProjGraphPrompts.DatabaseSchemaReview(@"D:\test.cs"),
            ProjGraphPrompts.ClassStructureReview(@"D:\test.cs")
        };

        foreach (var messages in prompts)
        {
            var list = messages.ToList();
            list[1].Text.Should().NotBeNullOrWhiteSpace("Assistant pre-fill should be non-empty");
        }
    }

    [Fact]
    public void AllPrompts_UserMessage_ShouldContain_MultiStepInstructions()
    {
        var prompts = new List<IEnumerable<ChatMessage>>
        {
            ProjGraphPrompts.ArchitectureReview(@"D:\test.slnx"),
            ProjGraphPrompts.DependencyAnalysis(@"D:\test.slnx"),
            ProjGraphPrompts.DatabaseSchemaReview(@"D:\test.cs"),
            ProjGraphPrompts.ClassStructureReview(@"D:\test.cs")
        };

        foreach (var messages in prompts)
        {
            var userMessage = messages.ToList()[0].Text;
            // Each prompt should have numbered steps
            userMessage.Should().Contain("1.");
            userMessage.Should().Contain("2.");
        }
    }
}
