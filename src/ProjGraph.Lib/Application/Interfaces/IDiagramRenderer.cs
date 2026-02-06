namespace ProjGraph.Lib.Application.Interfaces;

/// <summary>
/// Defines a contract for rendering models into a diagram string.
/// </summary>
/// <typeparam name="TModel">The type of model to render.</typeparam>
public interface IDiagramRenderer<in TModel>
{
    /// <summary>
    /// Renders the specified model into a string representation.
    /// </summary>
    /// <param name="model">The model to render.</param>
    /// <returns>A string representation of the diagram.</returns>
    string Render(TModel model);
}