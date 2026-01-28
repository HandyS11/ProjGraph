using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Interfaces;

/// <summary>
/// Interface for rendering a ClassModel into a specific format.
/// </summary>
public interface IClassDiagramRenderer
{
    /// <summary>
    /// Renders the provided ClassModel.
    /// </summary>
    /// <param name="model">The class diagram model.</param>
    /// <returns>The rendered representation (e.g., Mermaid string).</returns>
    string Render(ClassModel model);
}