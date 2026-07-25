using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.Sample.Services.Pipeline;

// Discovered by the convention scan purely by implementing IPipelineStep -- no attribute of their
// own, and no registration code anywhere. The default Mode is TryAddEnumerable, so all of them
// coexist and are injected together as IEnumerable<IPipelineStep>.
public sealed class ValidateStep : IPipelineStep
{
    public string Describe() => "validate";
}

public sealed class TransformStep : IPipelineStep
{
    public string Describe() => "transform";
}

// Explicit beats convention: because this class carries a [Service] attribute, the scan skips it
// entirely and only this registration applies -- which is also how a single class opts out of (or
// deviates from) the convention.
[Service(ServiceLifetime.Transient)]
public sealed class PersistStep : IPipelineStep
{
    private readonly Guid _id = Guid.NewGuid();

    public string Describe() => $"persist ({_id.ToString()[..8]})";
}

// Abstract, so the scan passes over it silently -- but the concrete class deriving from it
// implements IPipelineStep just as much as one that lists the interface itself, and is registered.
public abstract class AuditStepBase : IPipelineStep
{
    public abstract string Describe();
}

public sealed class AuditStep : AuditStepBase
{
    public override string Describe() => "audit";
}
