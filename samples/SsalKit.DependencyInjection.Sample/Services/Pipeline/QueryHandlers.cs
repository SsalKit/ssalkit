namespace SsalKit.DependencyInjection.Sample.Services.Pipeline;

// The other half of the convention scan: an *unbound* generic contract,
// [assembly: RegisterImplementationsOf(typeof(IQueryHandler<,>), ServiceLifetime.Scoped)].
// Each class below is registered under the specific closed instantiation(s) it implements, so
// IQueryHandler<CountQuery, int> and IQueryHandler<NameQuery, string> resolve independently
// without either being named anywhere in a registration.
public interface IQueryHandler<TQuery, TResult>
{
    TResult Handle(TQuery query);
}

public sealed record CountQuery(string Text);

public sealed record NameQuery(int Id);

public sealed class CountQueryHandler : IQueryHandler<CountQuery, int>
{
    public int Handle(CountQuery query) => query.Text.Length;
}

public sealed class NameQueryHandler : IQueryHandler<NameQuery, string>
{
    public string Handle(NameQuery query) => $"user-{query.Id}";
}
