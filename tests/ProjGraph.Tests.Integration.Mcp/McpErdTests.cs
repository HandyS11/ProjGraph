using ModelContextProtocol;
using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

public sealed class McpErdTests : IDisposable
{
    private readonly TestDirectory _temp = new();
    private readonly string _tempFile;

    public McpErdTests()
    {
        _tempFile = Path.Combine(_temp.DirectoryPath, "temp.cs");
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System.Collections.Generic;

                               namespace TestNamespace;

                               public class Blog
                               {
                                   public int Id { get; set; }
                                   public string Title { get; set; }
                                   public List<Post> Posts { get; set; }
                               }

                               public class Post
                               {
                                   public int Id { get; set; }
                                   public string Content { get; set; }
                                   public int BlogId { get; set; }
                                   public Blog Blog { get; set; }
                               }

                               public class MyDbContext : DbContext
                               {
                                   public DbSet<Blog> Blogs { get; set; }
                                   public DbSet<Post> Posts { get; set; }
                               }
                               """;
        File.WriteAllText(_tempFile, content);
    }

    private static string GetSamplePath(string relativePath)
    {
        return TestPathHelper.GetSamplePath(relativePath);
    }

    private static ProjGraphTools CreateTools()
    {
        return McpTestHelper.CreateTools();
    }

    [Fact]
    public async Task GetErd_SimpleDbContext_ShouldReturnValidMermaid()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetErdAsync(_tempFile);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("erDiagram");
        result.Should().Contain("Blog");
        result.Should().Contain("Post");
        result.Should().Contain("||--o{"); // Relationship
    }

    [Fact]
    public async Task GetErd_SimpleDbContext_ShouldShowProperties()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetErdAsync(_tempFile);

        // Assert
        result.Should().Contain("int Id");
        result.Should().Contain("string Title");
        result.Should().Contain("string Content");
        result.Should().Contain("int BlogId");
    }

    [Fact]
    public async Task GetErd_SimpleDbContext_ShouldShowRelationship()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetErdAsync(_tempFile);

        // Assert
        result.Should().Contain("Blog ||--o{ Post");
    }

    [Fact]
    public async Task GetErd_SimpleContext_ShouldGenerateCompleteErDiagram()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = await tools.GetErdAsync(contextPath);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("erDiagram");
        result.Should().Contain("Author {");
        result.Should().Contain("Book {");
        result.Should().Contain("Category {");
        result.Should().Contain("Publisher {");
        result.Should().Contain("Review {");
    }

    [Fact]
    public async Task GetErd_SimpleContext_ShouldShowAllProperties()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = await tools.GetErdAsync(contextPath);

        // Assert
        result.Should().Contain("int Id PK");
        result.Should().Contain("string Name");
        result.Should().Contain("string Title");
        result.Should().Contain("int Rating");
        result.Should().Contain("DateTime PublishedDate");
    }

    [Fact]
    public async Task GetErd_SimpleContext_ShouldShowOneToManyRelationships()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = await tools.GetErdAsync(contextPath);

        // Assert
        result.Should().Contain("||--o{"); // Required one-to-many notation
        // Publisher -> Book has a non-nullable FK and no explicit config: required one-to-many.
        result.Should().Contain("Publisher ||--o{ Book");
        // Review -> Book is configured .IsRequired(false): optional one-to-many.
        result.Should().Contain("Book |o--o{ Review");
    }

    [Fact]
    public async Task GetErd_SimpleContext_WithContextName_ShouldSucceed()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = await tools.GetErdAsync(contextPath, "MyDbContext");

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("erDiagram");
        result.Should().Contain("Author");
        result.Should().Contain("Book");
    }

    [Fact]
    public async Task GetErd_SimpleContext_ShouldShowForeignKeys()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath("erd/simple-context/EntityFramework/MyDbContext.cs");

        // Act
        var result = await tools.GetErdAsync(contextPath);

        // Assert
        result.Should().Contain("FK");
        result.Should().Contain("int PublisherId FK");
        result.Should().Contain("int BookId FK");
    }

    [Fact]
    public async Task GetErd_NonExistentFile_ShouldThrow()
    {
        // Arrange
        var tools = CreateTools();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cs");

        // Act
        var act = async () => await tools.GetErdAsync(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetErd_InvalidCsFile_ShouldThrow()
    {
        // Arrange
        var tools = CreateTools();
        var invalidFile = Path.Combine(_temp.DirectoryPath, "invalid.cs");
        await File.WriteAllTextAsync(invalidFile, "public class NotADbContext { }");

        // Act
        var act = async () => await tools.GetErdAsync(invalidFile);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetErd_NonCsFile_ShouldThrowMcpException()
    {
        // Arrange
        var tools = CreateTools();
        var nonCsFile = Path.Combine(_temp.DirectoryPath, "test.txt");
        await File.WriteAllTextAsync(nonCsFile, "Not a C# file");

        // Act
        var act = async () => await tools.GetErdAsync(nonCsFile);

        // Assert - McpException so the guidance reaches the client instead of a generic error
        (await act.Should().ThrowAsync<McpException>()).Which.Message.Should().Contain(".cs");
    }

    [Fact]
    public async Task GetErd_InvalidOwnedMode_ShouldThrowMcpException()
    {
        // Arrange — unlike the CLI's Spectre validation, an unrecognized ownedMode previously fell through
        // to a silent default of MirrorEf. An MCP caller is usually an LLM, for which plausible-but-wrong
        // output with no signal is the worst failure mode, so this must now fail loudly instead.
        var tools = CreateTools();

        // Act
        var act = async () => await tools.GetErdAsync(_tempFile, ownedMode: "bogus");

        // Assert
        (await act.Should().ThrowAsync<McpException>()).Which.Message.Should().Contain("ownedMode");
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _temp.Dispose();
        }
    }
}
