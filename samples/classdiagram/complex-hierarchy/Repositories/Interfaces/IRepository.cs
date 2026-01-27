namespace ComplexHierarchy.Repositories.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> FindAsync(Guid id);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}