namespace SimpleHierarchy.Interfaces;

public interface IRepository<T> where T : class
{
    T? GetById(Guid id);
    IEnumerable<T> GetAll();
    void Save(T entity);
}
