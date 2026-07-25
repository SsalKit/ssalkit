// SsalKit.DependencyInjection sample
//
// The AddSsalKitDependencyInjectionSampleServices() extension method called below is produced entirely by the
// SsalKit.DependencyInjection source generator at build time -- nothing here registers services
// by hand. To see exactly what code was generated for this project, build it and look under:
//
//   obj/Debug/net10.0/generated/SsalKit.DependencyInjection.Generator/
//     SsalKit.DependencyInjection.Generator.ServiceRegistrationGenerator/SsalKitDependencyInjectionSampleServiceCollectionExtensions.g.cs
//
// (EmitCompilerGeneratedFiles + CompilerGeneratedFilesOutputPath are set in SsalKit.DependencyInjection.Sample.csproj
// specifically so the generated file lands on disk for inspection.)

using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.Sample.Services.Clock;
using SsalKit.DependencyInjection.Sample.Services.Greeting;
using SsalKit.DependencyInjection.Sample.Services.Messaging;
using SsalKit.DependencyInjection.Sample.Services.Notifications;
using SsalKit.DependencyInjection.Sample.Services.Repository;
using SsalKit.DependencyInjection.Sample.Services.Session;
using SsalKit.DependencyInjection.Sample.Services.Startup;

var services = new ServiceCollection();
services.AddSsalKitDependencyInjectionSampleServices();

using var provider = services.BuildServiceProvider();

Console.WriteLine("== SsalKit.DependencyInjection sample ==");
Console.WriteLine();

// Singleton: no lifetime argument is specified on [Service], so this relies on the default
// (ServiceLifetime.Singleton) -- every resolution (even from a different scope) returns the
// same instance.
var greeterFirst = provider.GetRequiredService<IGreetingService>();
var greeterSecond = provider.GetRequiredService<IGreetingService>();
Console.WriteLine($"[Singleton]      IGreetingService -> same instance every time: {ReferenceEquals(greeterFirst, greeterSecond)}");
Console.WriteLine($"                 {greeterFirst.Greet("SsalKit")}");
Console.WriteLine();

// Singleton registered with Mode = TryAdd: since nothing else registers IClock first, the
// generated registration wins and SystemClock is resolved.
var clock = provider.GetRequiredService<IClock>();
Console.WriteLine($"[Singleton/TryAdd] IClock -> resolved as {clock.GetType().Name}, Now = {clock.Now():O}");
Console.WriteLine();

// Transient, no interface implemented -> registered as itself; a new instance comes back from
// every resolution.
var builderFirst = provider.GetRequiredService<MessageBuilder>();
var builderSecond = provider.GetRequiredService<MessageBuilder>();
Console.WriteLine($"[Transient]      MessageBuilder -> new instance every time: {!ReferenceEquals(builderFirst, builderSecond)}");
Console.WriteLine($"                 {builderFirst.Build("Hello", "Sample")}");
Console.WriteLine();

// Keyed services: two implementations of the same interface, resolved independently by key.
var loudFormatter = provider.GetRequiredKeyedService<IMessageFormatter>("loud");
var quietFormatter = provider.GetRequiredKeyedService<IMessageFormatter>("quiet");
Console.WriteLine("[Keyed]          IMessageFormatter");
Console.WriteLine($"                 \"loud\"  -> {loudFormatter.Format("ssalkit is ready")}");
Console.WriteLine($"                 \"quiet\" -> {quietFormatter.Format("ssalkit is ready")}");
Console.WriteLine();

// Scoped: the same instance is shared within one scope, but differs across scopes.
using (var scopeA = provider.CreateScope())
using (var scopeB = provider.CreateScope())
{
    var sessionA1 = scopeA.ServiceProvider.GetRequiredService<ISessionContext>();
    var sessionA2 = scopeA.ServiceProvider.GetRequiredService<ISessionContext>();
    var sessionB = scopeB.ServiceProvider.GetRequiredService<ISessionContext>();

    Console.WriteLine($"[Scoped]         ISessionContext -> same instance within a scope: {ReferenceEquals(sessionA1, sessionA2)}");
    Console.WriteLine($"                 ISessionContext -> different instance across scopes: {!ReferenceEquals(sessionA1, sessionB)}");
    Console.WriteLine($"                 scope A session id: {sessionA1.SessionId}");
    Console.WriteLine($"                 scope B session id: {sessionB.SessionId}");
}
Console.WriteLine();

// Open generic: Repository<T> is [Service]'d once, but MEDI resolves and caches a separate
// Singleton instance per closed T requested -- IRepository<Order> and IRepository<Customer> are
// independent, and asking for IRepository<Order> twice returns the exact same instance.
var orderRepository = provider.GetRequiredService<IRepository<Order>>();
var orderRepositoryAgain = provider.GetRequiredService<IRepository<Order>>();
var customerRepository = provider.GetRequiredService<IRepository<Customer>>();

orderRepository.Add(new Order(1, "Keyboard"));
orderRepository.Add(new Order(2, "Mouse"));
customerRepository.Add(new Customer(1, "Ada"));

Console.WriteLine("[Open generic]   IRepository<T>");
Console.WriteLine($"                 IRepository<Order> -> same instance every time: {ReferenceEquals(orderRepository, orderRepositoryAgain)}");
Console.WriteLine($"                 IRepository<Order> vs IRepository<Customer> -> distinct instances: {!ReferenceEquals(orderRepository, customerRepository)}");
Console.WriteLine($"                 orders: [{string.Join(", ", orderRepository.GetAll())}]");
Console.WriteLine($"                 customers: [{string.Join(", ", customerRepository.GetAll())}]");
Console.WriteLine();

// Factory: StartupBanner has a private constructor, so this instance could only have come from
// the static factory method named via Factory = nameof(Create) -- the factory pulled IClock out
// of the IServiceProvider it was handed to stamp the text below.
var banner = provider.GetRequiredService<IStartupBanner>();
Console.WriteLine("[Factory]        IStartupBanner -> constructed by StartupBanner.Create(IServiceProvider)");
Console.WriteLine($"                 {banner.Text}");
Console.WriteLine();

// ServiceFactory: nothing in this project implements INotificationSenderFactory -- the generator
// emitted an implementation that forwards to GetRequiredKeyedService<INotificationSender>(channel)
// and registered it as a Singleton, so the interface resolves like any other service. The enum
// value is used verbatim as the keyed-service key, which is what makes it line up with the
// [Service(Key = NotificationChannel.Email)] registrations.
var senderFactory = provider.GetRequiredService<INotificationSenderFactory>();
Console.WriteLine("[ServiceFactory] INotificationSenderFactory -> generated implementation");
Console.WriteLine($"                 resolved as {senderFactory.GetType().Name}");
Console.WriteLine($"                 Email -> {senderFactory.Create(NotificationChannel.Email).Send("deploy finished")}");
Console.WriteLine($"                 Sms   -> {senderFactory.Create(NotificationChannel.Sms).Send("deploy finished")}");

// NotificationChannel.Push has no [Service(Key = ...)] registration anywhere, and the factory adds
// no fallback of its own: whatever GetRequiredKeyedService throws is the factory's contract too.
try
{
    senderFactory.Create(NotificationChannel.Push);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"                 Push  -> {ex.GetType().Name}: no service registered for this key");
}
