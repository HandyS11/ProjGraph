namespace ProjGraph.Tests.Integration.Helpers;

public sealed class TestDirectory : IDisposable
{
    public string DirectoryPath { get; }

    public TestDirectory()
    {
        // Use a subfolder in temp to avoid cluttering the root temp folder
        DirectoryPath = Path.Combine(Path.GetTempPath(), "ProjGraphTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(DirectoryPath);
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