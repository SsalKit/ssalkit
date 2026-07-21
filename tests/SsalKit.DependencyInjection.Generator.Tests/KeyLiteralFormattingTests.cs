using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// End-to-end tests (through the real generator) for <c>KeyLiteralFormatter</c>'s handling of
/// every primitive attribute-argument type it can be asked to format, plus the fallback numeric
/// cast used for enum values that don't match a defined member.
/// </summary>
public class KeyLiteralFormattingTests
{
    private const string Usings = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public void CharKey_FormatsAsCharLiteral()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = 'x')]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>('x');", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void BoolKey_FormatsAsBooleanLiteral()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = true)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(true);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void ByteKey_FormatsWithByteCast()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = (byte)5)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>((byte)5);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void SByteKey_FormatsWithSByteCast()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = (sbyte)-5)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>((sbyte)-5);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void ShortKey_FormatsWithShortCast()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = (short)-5)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>((short)-5);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void UShortKey_FormatsWithUShortCast()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = (ushort)5)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>((ushort)5);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void UIntKey_FormatsWithUSuffix()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = 5U)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(5U);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void LongKey_FormatsWithLSuffix()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = 5L)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(5L);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void ULongKey_FormatsWithULSuffix()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = 5UL)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(5UL);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void FloatKey_FormatsWithFSuffix()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = 5.5F)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(5.5F);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void DoubleKey_FormatsWithDSuffix()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = 5.5D)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(5.5D);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void EnumKey_UndefinedValue_FallsBackToNumericCast()
    {
        const string source = Usings + """
            using System;

            namespace TestNs;

            [Flags]
            public enum Color { None = 0, Red = 1, Blue = 2 }

            public interface IFoo { }

            [Service(Key = (Color)3)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("(global::TestNs.Color)(3)", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void EnumKey_UlongUnderlyingType_UndefinedHighBitValue_FormatsWithoutOverflow()
    {
        // Regression test: the previous implementation funneled every fallback enum value through
        // Convert.ToInt64, which throws OverflowException for a ulong value using the top bit
        // (anything >= 2^63, i.e. larger than long.MaxValue).
        const string source = Usings + """
            using System;

            namespace TestNs;

            [Flags]
            public enum BigFlags : ulong
            {
                None = 0,
                High = 1UL << 63,
            }

            public interface IFoo { }

            [Service(Key = (BigFlags)((1UL << 63) | (1UL << 62)))]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("(global::TestNs.BigFlags)(13835058055282163712UL)", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void EnumKey_MemberNamedDefault_EscapesWithAtPrefix()
    {
        // Regression test: an enum member literally named `default` (or any other reserved
        // keyword) must be emitted as `@default`, since `TestNs.Mode.default` is a syntax error.
        const string source = Usings + """
            namespace TestNs;

            public enum Mode
            {
                @default,
                custom,
            }

            public interface IFoo { }

            [Service(Key = Mode.@default)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("global::TestNs.Mode.@default", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void DoubleKey_NaN_FormatsAsFrameworkConstant()
    {
        // Regression test: "R"-formatting double.NaN produces the string "NaN", which combined
        // with the "D" suffix used for every other double literal yields the illegal token "NaND".
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = double.NaN)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(global::System.Double.NaN);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void DoubleKey_NegativeInfinity_FormatsAsFrameworkConstant()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = double.NegativeInfinity)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(global::System.Double.NegativeInfinity);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void FloatKey_PositiveInfinity_FormatsAsFrameworkConstant()
    {
        // Regression test: "R"-formatting float.PositiveInfinity produces "Infinity", which
        // combined with the "F" suffix yields the illegal token "InfinityF".
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = float.PositiveInfinity)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(global::System.Single.PositiveInfinity);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void FloatKey_NaN_FormatsAsFrameworkConstant()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = float.NaN)]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(global::System.Single.NaN);", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void TypeKey_FormatsAsTypeofExpression()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IMarker { }

            [Service(As = typeof(IFoo), Key = typeof(IMarker))]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains(
            "services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(typeof(global::TestNs.IMarker));",
            generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void TypeKey_ClosedGenericType_FormatsCorrectly()
    {
        const string source = Usings + """
            using System.Collections.Generic;

            namespace TestNs;

            public interface IFoo { }

            [Service(Key = typeof(List<int>))]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("typeof(global::System.Collections.Generic.List<int>)", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void TypeKey_UnboundGenericType_FormatsCorrectly()
    {
        // Regression test: Key = typeof(List<>) (an open/unbound generic type reference) is a
        // valid attribute argument that the previous implementation silently ignored entirely
        // (TypedConstantKind.Type was unhandled), downgrading the keyed registration request to a
        // non-keyed one.
        const string source = Usings + """
            using System.Collections.Generic;

            namespace TestNs;

            public interface IFoo { }

            [Service(Key = typeof(List<>))]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("typeof(global::System.Collections.Generic.List<>)", generated);
        Assert.Contains("AddKeyedSingleton", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }
}
