using DesignPatterns.Base;
using DesignPatterns.Domain;
using DesignPatterns.Interfaces;

namespace DesignPatterns.Repositories;

/// <summary>
/// Unit of Work implementation for transaction management
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = new();
    private bool _disposed = false;

    public IRepository<T> GetRepository<T>() where T : class, IEntity
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
        {
            // Create specialized repositories if available
            if (type == typeof(User))
            {
                _repositories[type] = new UserRepository();
            }
            else if (type == typeof(Order))
            {
                _repositories[type] = new OrderRepository();
            }
            else
            {
                _repositories[type] = new Repository<T>();
            }
        }

        return (IRepository<T>)_repositories[type];
    }

    public async Task<int> SaveChangesAsync()
    {
        // In a real application, this would save to database
        await Task.CompletedTask;
        return 1;
    }

    public async Task BeginTransactionAsync()
    {
        await Task.CompletedTask;
        // Begin database transaction
    }

    public async Task CommitAsync()
    {
        await Task.CompletedTask;
        // Commit database transaction
    }

    public async Task RollbackAsync()
    {
        await Task.CompletedTask;
        // Rollback database transaction
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _repositories.Clear();
        }

        _disposed = true;
    }
}