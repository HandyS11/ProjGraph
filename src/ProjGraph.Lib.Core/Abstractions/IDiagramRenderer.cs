namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// Defines a contract for rendering models into a diagram string.
/// </summary>
/// <typeparam name="TModel">The type of model to render.</typeparam>
public interface IDiagramRenderer<in TModel>
{
    /// <summary>
    /// Gets the format name this renderer produces (e.g., "mermaid", "tree", "flat").
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Renders the specified model into a string representation.
    /// </summary>
    /// <param name="model">The model to render.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    /// <returns>A string representation of the diagram.</returns>
    string Render(TModel model, DiagramOptions? options = null);
}