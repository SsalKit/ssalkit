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
}
