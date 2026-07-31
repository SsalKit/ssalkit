using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.Determinism.Analyzer.Diagnostics;

namespace SsalKit.Determinism.Analyzer;

/// <summary>
/// The fixed v1 catalog of non-deterministic APIs, resolved to symbols once per compilation.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is declared as metadata names plus member selectors, never as symbols, so that
/// <b>a type the compilation does not reference is silently skipped</b>. That is what lets the
/// entries for <c>SsalKit.Randomness</c> exist here while this package keeps zero package
/// dependencies: they join the ban list only in a compilation that already references Randomness,
/// and in every other compilation they simply do not resolve.
/// </para>
/// <para>
/// Resolution happens once, at compilation start, into a symbol-keyed dictionary. Per operation the
/// analyzer then does one lookup on the operation's <see cref="ISymbol.OriginalDefinition"/> --
/// which is what makes a generic member (<c>HashCode.Combine&lt;T1, T2&gt;</c>,
/// <c>Task.Run&lt;T&gt;</c>) and a reduced extension-method call (<c>source.AsParallel()</c>) match
/// the single definition recorded here.
/// </para>
/// <para>
/// The catalog is deliberately closed: extending it per project is what
/// <c>Microsoft.CodeAnalysis.BannedApiAnalyzers</c> is for. What this package adds instead is the
/// opt-in scope and the concrete replacement named in each message.
/// </para>
/// </remarks>
internal sealed class BannedApiCatalog
{
    /// <summary>
    /// The member name Roslyn gives an instance constructor, so an entry can ban
    /// <c>new SomeType(...)</c> the same way it bans a method.
    /// </summary>
    private const string ConstructorName = ".ctor";

    /// <summary>
    /// Selects every member of the type -- for types where no member is deterministic, so listing
    /// them would only risk missing one.
    /// </summary>
    private const string AllMembers = "*";

    private static readonly CatalogEntry[] Entries = BuildEntries();

    private readonly Dictionary<ISymbol, DiagnosticDescriptor> _bannedMembers;

    private BannedApiCatalog(Dictionary<ISymbol, DiagnosticDescriptor> bannedMembers) =>
        _bannedMembers = bannedMembers;

    /// <summary>
    /// Whether nothing at all resolved, so the analyzer has nothing to compare against.
    /// </summary>
    public bool IsEmpty => _bannedMembers.Count == 0;

    /// <summary>
    /// Resolves every catalog entry that this compilation can see.
    /// </summary>
    /// <param name="compilation">The compilation under analysis.</param>
    /// <returns>The resolved catalog.</returns>
    public static BannedApiCatalog Create(Compilation compilation)
    {
        var bannedMembers = new Dictionary<ISymbol, DiagnosticDescriptor>(SymbolEqualityComparer.Default);

        foreach (var entry in Entries)
        {
            var type = compilation.GetTypeByMetadataName(entry.TypeMetadataName);

            if (type is null)
            {
                // Not referenced by this compilation (or ambiguously defined by several of its
                // references): the entry simply does not apply here.
                continue;
            }

            foreach (var member in SelectMembers(type, entry.MemberName))
            {
                // First entry wins, so a type that appears twice with different categories (Thread's
                // Sleep vs. CurrentThread, Environment's TickCount vs. MachineName) stays well
                // defined even if a future edit made two selectors overlap.
                if (!bannedMembers.ContainsKey(member.OriginalDefinition))
                {
                    bannedMembers.Add(member.OriginalDefinition, entry.Descriptor);
                }
            }
        }

        return new BannedApiCatalog(bannedMembers);
    }

    /// <summary>
    /// The descriptor to report for <paramref name="symbol"/>, or <see langword="null"/> when it is
    /// not in the catalog.
    /// </summary>
    /// <param name="symbol">The referenced member.</param>
    /// <returns>The matching descriptor, or <see langword="null"/>.</returns>
    public DiagnosticDescriptor? Find(ISymbol symbol) =>
        _bannedMembers.TryGetValue(Normalize(symbol), out var descriptor) ? descriptor : null;

    /// <summary>
    /// Reduces a referenced symbol to the definition the catalog records.
    /// </summary>
    /// <remarks>
    /// A call to a generic member yields a constructed symbol, and a call written in extension form
    /// (<c>source.AsParallel()</c>) yields a <em>reduced</em> method whose
    /// <see cref="ISymbol.OriginalDefinition"/> is the reduced definition rather than the static
    /// method the catalog holds -- hence <see cref="IMethodSymbol.ReducedFrom"/> first.
    /// </remarks>
    private static ISymbol Normalize(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method && method.ReducedFrom is not null)
        {
            return method.ReducedFrom.OriginalDefinition;
        }

        return symbol.OriginalDefinition;
    }

    private static ImmutableArray<ISymbol> SelectMembers(INamedTypeSymbol type, string memberName) =>
        memberName == AllMembers ? type.GetMembers() : type.GetMembers(memberName);

    private static CatalogEntry[] BuildEntries()
    {
        var ambientTime = DiagnosticDescriptors.AmbientTime;
        var randomness = DiagnosticDescriptors.NonDeterministicRandomness;
        var guids = DiagnosticDescriptors.GuidGeneration;
        var hashing = DiagnosticDescriptors.RandomizedHashing;
        var environment = DiagnosticDescriptors.EnvironmentIdentity;
        var scheduling = DiagnosticDescriptors.SchedulingAndParallelism;

        return new[]
        {
            // SSALD001 -- ambient time.
            new CatalogEntry("System.DateTime", "Now", ambientTime),
            new CatalogEntry("System.DateTime", "UtcNow", ambientTime),
            new CatalogEntry("System.DateTime", "Today", ambientTime),
            new CatalogEntry("System.DateTimeOffset", "Now", ambientTime),
            new CatalogEntry("System.DateTimeOffset", "UtcNow", ambientTime),
            // Only the ambient singleton: a TimeProvider that was injected is the recommended fix,
            // so calling GetUtcNow() on one must stay silent.
            new CatalogEntry("System.TimeProvider", "System", ambientTime),
            new CatalogEntry("System.Diagnostics.Stopwatch", "StartNew", ambientTime),
            new CatalogEntry("System.Diagnostics.Stopwatch", "GetTimestamp", ambientTime),
            new CatalogEntry("System.Diagnostics.Stopwatch", ConstructorName, ambientTime),
            new CatalogEntry("System.Environment", "TickCount", ambientTime),
            new CatalogEntry("System.Environment", "TickCount64", ambientTime),

            // SSALD002 -- randomness.
            new CatalogEntry("System.Random", "Shared", randomness),
            // Both constructors, the seeded one included: System.Random's algorithm is not part of
            // its contract and has changed between runtime versions, so even a fixed seed does not
            // reproduce a sequence across processes or versions.
            new CatalogEntry("System.Random", ConstructorName, randomness),
            new CatalogEntry("System.Security.Cryptography.RandomNumberGenerator", "Create", randomness),
            new CatalogEntry("System.Security.Cryptography.RandomNumberGenerator", "Fill", randomness),
            new CatalogEntry("System.Security.Cryptography.RandomNumberGenerator", "GetBytes", randomness),
            new CatalogEntry("System.Security.Cryptography.RandomNumberGenerator", "GetNonZeroBytes", randomness),
            new CatalogEntry("System.Security.Cryptography.RandomNumberGenerator", "GetInt32", randomness),
            new CatalogEntry("System.Security.Cryptography.RandomNumberGenerator", "GetHexString", randomness),
            new CatalogEntry("System.Security.Cryptography.RandomNumberGenerator", "GetString", randomness),
            new CatalogEntry("System.Security.Cryptography.RandomNumberGenerator", "GetItems", randomness),
            new CatalogEntry("System.Security.Cryptography.RandomNumberGenerator", "Shuffle", randomness),
            new CatalogEntry("System.IO.Path", "GetRandomFileName", randomness),
            // Resolved only when SsalKit.Randomness is referenced -- this package depends on nothing.
            // Its own non-deterministic entry points get no exemption: dogfooding cuts both ways.
            new CatalogEntry("SsalKit.Randomness.SharedRandomSource", "Instance", randomness),
            new CatalogEntry("SsalKit.Randomness.CryptoRandomSource", "Instance", randomness),
            new CatalogEntry("SsalKit.Randomness.DeterministicRandom", "CreateRandomlySeeded", randomness),

            // SSALD003 -- identifier generation.
            new CatalogEntry("System.Guid", "NewGuid", guids),
            new CatalogEntry("System.Guid", "CreateVersion7", guids),

            // SSALD004 -- per-process randomized hashing. Only the framework's own randomized
            // implementations are listed, so a call that resolves to a user-written override of
            // GetHashCode is not reported: that implementation is analyzed on its own terms.
            new CatalogEntry("System.Object", "GetHashCode", hashing),
            new CatalogEntry("System.ValueType", "GetHashCode", hashing),
            new CatalogEntry("System.String", "GetHashCode", hashing),
            new CatalogEntry("System.StringComparer", "GetHashCode", hashing),
            new CatalogEntry("System.HashCode", AllMembers, hashing),

            // SSALD005 -- environment, process, and thread identity.
            new CatalogEntry("System.Environment", "MachineName", environment),
            new CatalogEntry("System.Environment", "UserName", environment),
            new CatalogEntry("System.Environment", "UserDomainName", environment),
            new CatalogEntry("System.Environment", "ProcessId", environment),
            new CatalogEntry("System.Environment", "CurrentManagedThreadId", environment),
            new CatalogEntry("System.Environment", "ProcessorCount", environment),
            new CatalogEntry("System.Environment", "WorkingSet", environment),
            new CatalogEntry("System.Environment", "CommandLine", environment),
            new CatalogEntry("System.Environment", "CurrentDirectory", environment),
            new CatalogEntry("System.Environment", "GetEnvironmentVariable", environment),
            new CatalogEntry("System.Environment", "GetEnvironmentVariables", environment),
            new CatalogEntry("System.Diagnostics.Process", "GetCurrentProcess", environment),
            new CatalogEntry("System.Threading.Thread", "CurrentThread", environment),
            new CatalogEntry("System.IO.Path", "GetTempPath", environment),
            new CatalogEntry("System.IO.Path", "GetTempFileName", environment),

            // SSALD006 -- scheduling and parallelism.
            new CatalogEntry("System.Threading.Tasks.Task", "Run", scheduling),
            new CatalogEntry("System.Threading.Tasks.Task", "Delay", scheduling),
            new CatalogEntry("System.Threading.Tasks.Task", "WhenAny", scheduling),
            new CatalogEntry("System.Threading.Tasks.Task", "Yield", scheduling),
            // Task.Factory.StartNew binds to TaskFactory, so that is where the ban belongs; the
            // generic form is reached through Task<T>.Factory.
            new CatalogEntry("System.Threading.Tasks.TaskFactory", "StartNew", scheduling),
            new CatalogEntry("System.Threading.Tasks.TaskFactory`1", "StartNew", scheduling),
            new CatalogEntry("System.Threading.Thread", "Sleep", scheduling),
            new CatalogEntry("System.Threading.ThreadPool", "QueueUserWorkItem", scheduling),
            new CatalogEntry("System.Threading.Tasks.Parallel", "For", scheduling),
            new CatalogEntry("System.Threading.Tasks.Parallel", "ForEach", scheduling),
            new CatalogEntry("System.Threading.Tasks.Parallel", "Invoke", scheduling),
            new CatalogEntry("System.Threading.Tasks.Parallel", "ForAsync", scheduling),
            new CatalogEntry("System.Threading.Tasks.Parallel", "ForEachAsync", scheduling),
            new CatalogEntry("System.Linq.ParallelEnumerable", "AsParallel", scheduling),
            new CatalogEntry("System.Threading.Timer", ConstructorName, scheduling),
            new CatalogEntry("System.Timers.Timer", ConstructorName, scheduling),
        };
    }

    /// <summary>
    /// One catalog row: a type by metadata name, a member selector, and the category it belongs to.
    /// </summary>
    private readonly struct CatalogEntry
    {
        public CatalogEntry(string typeMetadataName, string memberName, DiagnosticDescriptor descriptor)
        {
            TypeMetadataName = typeMetadataName;
            MemberName = memberName;
            Descriptor = descriptor;
        }

        public string TypeMetadataName { get; }

        public string MemberName { get; }

        public DiagnosticDescriptor Descriptor { get; }
    }
}
