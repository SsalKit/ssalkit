namespace SsalKit.DependencyInjection.Sample.Services.Pipeline;

// The convention contract: nothing below carries [Service]. The single
// [assembly: RegisterImplementationsOf(typeof(IPipelineStep))] line in Program.cs is what registers
// every implementation of this interface declared in this project.
public interface IPipelineStep
{
    string Describe();
}
