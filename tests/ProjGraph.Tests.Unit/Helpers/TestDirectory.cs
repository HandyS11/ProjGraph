namespace ProjGraph.Tests.Unit.Helpers;

/// <summary>
/// A helper class to manage temporary directories for testing.
/// Ensures each test has a unique, isolated environment and handles cleanup.
/// </summary>
public sealed class TestDirectory : IDisposable
{
    public string DirectoryPath { get; }

    public TestDirectory()
    {
        // Use a subfolder in temp to avoid cluttering the root temp folder
        DirectoryPath = Path.Combine(Path.GetTempPath(), "ProjGraphTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>
    /// Creates a file with the specified name and content within the temporary directory.
    /// </summary>
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
    public string GetTempFilePath(string extension = ".tmp")
    {
        return Path.Combine(DirectoryPath, $"{Guid.NewGuid()}{extension}");
    }

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