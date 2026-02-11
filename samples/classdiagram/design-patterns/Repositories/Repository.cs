using DesignPatterns.Base;
using DesignPatterns.Interfaces;
using System.Linq.Expressions;

namespace DesignPatterns.Repositories;

/// <summary>
/// Generic repository implementation
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public class Repository<T> : IRepository<T> where T : class, IEntity
{
    // In a real application, this would use a DbContext
    private readonly List<T> _data = new();

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        await Task.CompletedTask;
        return _data.FirstOrDefault(e => e.Id == id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        await Task.CompletedTask;
        return _data.ToList();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        await Task.CompletedTask;
        return _data.AsQueryable().Where(predicate).ToList();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await Task.CompletedTask;
        _data.Add(entity);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        await Task.CompletedTask;
        var existing = _data.FirstOrDefault(e => e.Id == entity.Id);
        if (existing != null)
        {
            _data.Remove(existing);
            _data.Add(entity);
        }
    }

    public virtual async Task DeleteAsync(int id)
    {
        await Task.CompletedTask;
        var entity = _data.FirstOrDefault(e => e.Id == id);
        if (entity != null)
        {
            _data.Remove(entity);
        }
    }

    public virtual async Task<int> CountAsync()
    {
        await Task.CompletedTask;
        return _data.Count;
    }
}
