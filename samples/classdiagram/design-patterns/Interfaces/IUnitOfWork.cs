using DesignPatterns.Base;

namespace DesignPatterns.Interfaces;

/// <summary>
/// Unit of Work pattern for transaction management
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IRepository<T> GetRepository<T>() where T : class, IEntity;
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}