using ProjGraph.Lib.Core.Infrastructure;

namespace ProjGraph.Tests.Unit.Core;

public sealed class PhysicalFileSystemTests : IDisposable
{
    private readonly string _testDirPath;

    public PhysicalFileSystemTests()
    {
        _testDirPath = Path.Combine(Path.GetTempPath(), "ProjGraphTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirPath))
        {
            Directory.Delete(_testDirPath, true);
        }
    }

    [Fact]
    public void CreateDirectory_CreatesPath()
    {
        // Arrange
        var fs = new PhysicalFileSystem();
        var newDir = Path.Combine(_testDirPath, "subdir", "nested");

        // Act
        fs.CreateDirectory(newDir);

        // Assert
        Directory.Exists(newDir).Should().BeTrue();
    }

    [Fact]
    public async Task WriteAllTextAsync_WritesContent_Utf8NoBom()
    {
        // Arrange
        var fs = new PhysicalFileSystem();
        var filePath = Path.Combine(_testDirPath, "testfile.txt");
        const string content = "Hello World! 🚀";

        // Act
        await fs.WriteAllTextAsync(filePath, content);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var readContent = await File.ReadAllTextAsync(filePath);
        readContent.Should().Be(content);

        // Verify no BOM
        var bytes = await File.ReadAllBytesAsync(filePath);
        // UTF-8 BOM is EF BB BF
        if (bytes.Length >= 3)
        {
            (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF).Should().BeFalse("BOM should not be present");
        }
    }

    [Fact]
    public void FileExists_ReturnsCorrectResult()
    {
        // Arrange
        var fs = new PhysicalFileSystem();
        var filePath = Path.Combine(_testDirPath, "exists.txt");
        File.WriteAllText(filePath, "test");

        // Act & Assert
        fs.FileExists(filePath).Should().BeTrue();
        fs.FileExists(Path.Combine(_testDirPath, "notexists.txt")).Should().BeFalse();
    }
}
