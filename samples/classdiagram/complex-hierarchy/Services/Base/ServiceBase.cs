using ComplexHierarchy.Repositories.Interfaces;
using ComplexHierarchy.Services.Interfaces;

namespace ComplexHierarchy.Services.Base;

public abstract class ServiceBase<T>(IRepository<T> repository) : IService<T>
    where T : class
{
    protected readonly IRepository<T> _repository = repository;

    public virtual async Task<T?> GetByIdAsync(Guid id)
    {
        return await _repository.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await Task.FromResult(new List<T>());
    }
}
