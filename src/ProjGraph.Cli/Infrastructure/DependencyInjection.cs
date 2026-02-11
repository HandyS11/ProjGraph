using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace ProjGraph.Cli.Infrastructure;

/// <summary>
/// A custom implementation of the ITypeRegistrar interface for integrating Microsoft.Extensions.DependencyInjection
/// with Spectre.Console.Cli. This class is responsible for registering services and building a type resolver.
/// </summary>
/// <param name="builder">The IServiceCollection used to register services.</param>
internal sealed class TypeRegistrar(IServiceCollection builder) : ITypeRegistrar
{
    /// <summary>
    /// Builds and returns an instance of ITypeResolver using the registered services.
    /// </summary>
    /// <returns>An ITypeResolver instance that resolves types using the built service provider.</returns>
    public ITypeResolver Build()
    {
        return new TypeResolver(builder.BuildServiceProvider());
    }

    /// <summary>
    /// Registers a service and its implementation as a singleton in the service collection.
    /// </summary>
    /// <param name="service">The type of the service to register.</param>
    /// <param name="implementation">The type of the implementation to register.</param>
    public void Register(Type service, Type implementation)
    {
        builder.AddSingleton(service, implementation);
    }

    /// <summary>
    /// Registers a specific instance of a service in the service collection.
    /// </summary>
    /// <param name="service">The type of the service to register.</param>
    /// <param name="implementation">The instance of the service to register.</param>
    public void RegisterInstance(Type service, object implementation)
    {
        builder.AddSingleton(service, implementation);
    }

    /// <summary>
    /// Registers a service with a factory method for lazy initialization in the service collection.
    /// </summary>
    /// <param name="service">The type of the service to register.</param>
    /// <param name="factory">A factory method to create the service instance.</param>
    public void RegisterLazy(Type service, Func<object> factory)
    {
        builder.AddSingleton(service, _ => factory());
    }
}

/// <summary>
/// A custom implementation of the ITypeResolver interface for resolving types using a service provider.
/// </summary>
/// <param name="provider">The IServiceProvider used to resolve services.</param>
internal sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
    /// <summary>
    /// Resolves an instance of the specified type from the service provider.
    /// </summary>
    /// <param name="type">The type to resolve.</param>
    /// <returns>An instance of the specified type, or null if the type is not registered.</returns>
    public object? Resolve(Type? type)
    {
        if (type is null)
        {
            return null;
        }

        return provider.GetService(type);
    }

    /// <summary>
    /// Disposes the service provider if it implements IDisposable.
    /// </summary>
    public void Dispose()
    {
        if (provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
