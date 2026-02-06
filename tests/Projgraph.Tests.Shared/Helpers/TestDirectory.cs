namespace Projgraph.Tests.Shared.Helpers;

/// <summary>
/// A helper class to manage temporary directories for testing.
/// Ensures each test has a unique, isolated environment and handles cleanup.
/// </summary>
public sealed class TestDirectory : IDisposable
{
    /// <summary>
    /// Gets the full path to the temporary directory.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestDirectory"/> class.
    /// Creates a unique temporary directory for test isolation.
    /// </summary>
    public TestDirectory()
    {
        // Use a subfolder in temp to avoid cluttering the root temp folder
        DirectoryPath = Path.Combine(Path.GetTempPath(), "ProjGraphTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>
    /// Creates a file with the specified name and content within the temporary directory.
    /// Automatically creates any required subdirectories.
    /// </summary>
    /// <param name="fileName">The name or relative path of the file to create.</param>
    /// <param name="content">The content to write to the file.</param>
    /// <returns>The full path to the created file.</returns>
    public string CreateFile(string fileName, string content)
    {
        var filePath = Path.Combine(DirectoryPath, fileName);

        // Ensure subdirectories exist if fileName contains paths
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, content);
        return filePath;
    }

    /// <summary>
    /// Returns a unique path for a file that doesn't exist yet.
    /// </summary>
    /// <param name="extension">The file extension to use. Defaults to ".tmp".</param>
    /// <returns>A unique file path within the temporary directory.</returns>
    public string GetTempFilePath(string extension = ".tmp")
    {
        return Path.Combine(DirectoryPath, $"{Guid.NewGuid()}{extension}");
    }

    /// <summary>
    /// Disposes the temporary directory and all its contents.
    /// Attempts to perform cleanup with retry logic to handle Windows file locks.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                // Recursive delete to clean up all files and subdirectories
                Directory.Delete(DirectoryPath, true);
            }
        }
        catch (IOException)
        {
            // On Windows, file locks can cause temporary failures. 
            // We can try to wait and retry once.
            try
            {
                Thread.Sleep(100);
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch
            {
                // Best effort cleanup - CI will eventually clean up Temp folder or next run will use a different GUID
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}