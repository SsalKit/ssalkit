using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Repository;

// Open generic: unlike every other [Service] in this sample, Repository<T> itself has a type
// parameter. The generator emits a single Type-based registration --
// services.AddSingleton(typeof(IRepository<>), typeof(Repository<>)) -- and
// Microsoft.Extensions.DependencyInjection specializes it per closed T requested at resolution
// time, caching one Singleton instance per distinct T (so IRepository<Order> and
// IRepository<Customer> below are independent singletons, each with its own backing list).
[Service(ServiceLifetime.Singleton)]
public sealed class Repository<T> : IRepository<T>
{
    private readonly List<T> _items = new();

    public void Add(T item) => _items.Add(item);

    public IReadOnlyList<T> GetAll() => _items;
}
