using System;
using System.IO;
using System.Reflection;
using SsalKit.StableHashing.Generator.Tests.TestSupport;

namespace SsalKit.StableHashing.Generator.Tests;

/// <summary>
/// Actually compiles the generated code into a loadable in-memory assembly and *runs* it against a
/// contract whose members count their own getter invocations, to prove each
/// <c>[StableHashMember]</c> access happens exactly once per <c>ComputeStableHash</c> call --
/// unlike the snapshot tests, which only prove the emitted *text* looks right.
/// </summary>
/// <remarks>
/// Before the local-variable-caching fix, a nullable member's getter was read three times (once
/// for the null marker, once for the guard condition, once for the guarded value) and a collection
/// member's getter was read once per <c>AppendCount</c> call plus once per loop-condition check
/// plus once per element index -- for an <c>n</c>-element collection, roughly <c>2n + 2</c> calls
/// to a single property. A getter is not guaranteed side-effect-free or to return the same
/// reference twice (this test's collection member deliberately returns a fresh array each time it
/// is read, so a regression here would either fail this assertion or, for a real consumer, corrupt
/// the encoded byte stream against a mutating source), so re-reading it is a correctness bug, not
/// merely a performance one.
/// </remarks>
public class GetterEvaluationTests
{
    private const string Source = """
        using System.Collections.Generic;
        using SsalKit.StableHashing;

        namespace Game.Snapshots;

        public static class Counters
        {
            public static int NullableReads;
            public static int ItemsReads;
        }

        [StableHashContract("game.counting-contract", Version = 1)]
        public sealed class CountingContract
        {
            [StableHashMember(1)]
            public int? NullableValue
            {
                get
                {
                    Counters.NullableReads++;
                    return 42;
                }
            }

            [StableHashMember(2)]
            public IReadOnlyList<int?> Items
            {
                get
                {
                    Counters.ItemsReads++;
                    // A fresh array on every read: if the generated code re-reads this getter
                    // (for AppendCount, again for the loop bound, again per element), each read
                    // would hand back a *different* array instance, which is exactly the hazard
                    // single-evaluation caching exists to rule out.
                    return new int?[] { 1, null, 3 };
                }
            }
        }
        """;

    [Fact]
    public void ComputeStableHash_ReadsEachMemberGetterExactlyOnce()
    {
        var result = GeneratorTestSupport.RunGenerator(Source);
        result.AssertCompilesCleanly();

        using var stream = new MemoryStream();
        var emitResult = result.OutputCompilation.Emit(stream);
        Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));

        var assembly = Assembly.Load(stream.ToArray());

        var countersType = assembly.GetType("Game.Snapshots.Counters", throwOnError: true)!;
        var contractType = assembly.GetType("Game.Snapshots.CountingContract", throwOnError: true)!;
        var extensionsType = assembly.GetType("Game.Snapshots.CountingContractStableHashing", throwOnError: true)!;

        var instance = Activator.CreateInstance(contractType)!;
        var computeStableHash = extensionsType.GetMethod("ComputeStableHash", BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal(0, (int)countersType.GetField("NullableReads")!.GetValue(null)!);
        Assert.Equal(0, (int)countersType.GetField("ItemsReads")!.GetValue(null)!);

        computeStableHash.Invoke(null, [instance]);

        Assert.Equal(1, (int)countersType.GetField("NullableReads")!.GetValue(null)!);
        Assert.Equal(1, (int)countersType.GetField("ItemsReads")!.GetValue(null)!);

        // Calling it again must add exactly one more read each, not accumulate from residual state
        // -- confirms the count is per-call, not a fluke of a single run.
        computeStableHash.Invoke(null, [instance]);

        Assert.Equal(2, (int)countersType.GetField("NullableReads")!.GetValue(null)!);
        Assert.Equal(2, (int)countersType.GetField("ItemsReads")!.GetValue(null)!);
    }
}
