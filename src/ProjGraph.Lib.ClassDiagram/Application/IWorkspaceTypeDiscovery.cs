namespace ProjGraph.Lib.ClassDiagram.Application;

/// <summary>
/// Provides methods for discovering type definitions within a workspace by searching
/// directories and analyzing C# source files.
/// </summary>
public interface IWorkspaceTypeDiscovery
{
    /// <summary>
    /// Finds the file containing the definition of a specific type within a given directory or its subdirectories.
    /// </summary>
    /// <param name="typeName">The name of the type to search for (e.g., class, interface, struct, enum, or record).</param>
    /// <param name="startDirectory">The starting directory to begin the search.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the full path of the file
    /// containing the type definition if found; otherwise, null.
    /// </returns>
    Task<string?> FindTypeDefinitionFileAsync(string typeName, string startDirectory);
}
