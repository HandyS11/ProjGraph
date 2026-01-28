namespace ComplexHierarchy.Services.Interfaces;

public interface IService<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
}