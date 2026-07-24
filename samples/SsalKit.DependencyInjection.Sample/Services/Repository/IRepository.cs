namespace SsalKit.DependencyInjection.Sample.Services.Repository;

public interface IRepository<T>
{
    void Add(T item);

    IReadOnlyList<T> GetAll();
}
