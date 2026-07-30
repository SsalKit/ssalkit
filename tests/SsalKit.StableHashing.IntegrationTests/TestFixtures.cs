namespace SsalKit.StableHashing.IntegrationTests;

/// <summary>
/// Shared fixed instances and a hand-rolled encoding of <see cref="ComprehensiveContract"/>,
/// used by several test classes that need the exact same representative data.
/// </summary>
internal static class TestFixtures
{
    /// <summary>
    /// A fixed <see cref="ComprehensiveContract"/> instance exercising every v1-supported type
    /// (design doc §4.4) with non-default, non-boundary values, all populated collections, a
    /// present nullable value and reference member, and a nested contract member.
    /// </summary>
    public static ComprehensiveContract BuildComprehensiveInstance() => new()
    {
        Bool = true,
        Char = '한',
        SByte = -100,
        Byte = 200,
        Int16 = -12345,
        UInt16 = 54321,
        Int32 = -123456789,
        UInt32 = 3_000_000_000,
        Int64 = -1234567890123456789L,
        UInt64 = 12345678901234567890UL,
        Int128 = Int128.MinValue + 1,
        UInt128 = UInt128.MaxValue,
        Single = 3.14f,
        Double = 2.718281828,
        Decimal = 123.456m,
        String = "Hello, 세계!",
        Guid = new Guid("12345678-1234-5678-1234-567812345678"),
        DateOnly = new DateOnly(2026, 7, 30),
        TimeOnly = new TimeOnly(13, 45, 30),
        TimeSpan = TimeSpan.FromMinutes(90),
        DateTimeOffset = new DateTimeOffset(2026, 7, 30, 4, 0, 0, TimeSpan.Zero),
        NullableInt = 42,
        NullableString = "present",
        Array = [1, 2, 3],
        List = [4, 5],
        ReadOnlyList = new List<int> { 6, 7, 8 },
        Immutable = [9, 10],
        NestedPosition = new Position { X = 11, Y = -22 },
        Rarity = Rarity.Legendary,
    };

    /// <summary>
    /// Encodes <paramref name="value"/> by hand, calling <see cref="StableHashWriter"/> directly
    /// in the exact member-id-ascending order and per-type rule the generator emits for
    /// <see cref="ComprehensiveContract"/> (cross-checked against
    /// SsalKit.StableHashing.Generator.Tests.GeneratorSnapshotTests and the design doc §4.4
    /// table). This is a second, independent implementation of "how to encode a
    /// ComprehensiveContract" -- it does not call <c>ComprehensiveContract.AppendStableHash</c> or
    /// any other generated code -- so agreement between this method's result and
    /// <c>value.ComputeStableHash()</c> is a genuine end-to-end check that the generator followed
    /// the encoding contract, not just the generator agreeing with itself.
    /// </summary>
    public static StableHash64 EncodeManually(ComprehensiveContract value)
    {
        StableHashWriter writer = StableHashWriter.Create();
        writer.AppendContractHeader("integration.comprehensive", 1);

        writer.AppendMemberId(1);
        writer.AppendBoolean(value.Bool);

        writer.AppendMemberId(2);
        writer.AppendChar(value.Char);

        writer.AppendMemberId(3);
        writer.AppendSByte(value.SByte);

        writer.AppendMemberId(4);
        writer.AppendByte(value.Byte);

        writer.AppendMemberId(5);
        writer.AppendInt16(value.Int16);

        writer.AppendMemberId(6);
        writer.AppendUInt16(value.UInt16);

        writer.AppendMemberId(7);
        writer.AppendInt32(value.Int32);

        writer.AppendMemberId(8);
        writer.AppendUInt32(value.UInt32);

        writer.AppendMemberId(9);
        writer.AppendInt64(value.Int64);

        writer.AppendMemberId(10);
        writer.AppendUInt64(value.UInt64);

        writer.AppendMemberId(11);
        writer.AppendInt128(value.Int128);

        writer.AppendMemberId(12);
        writer.AppendUInt128(value.UInt128);

        writer.AppendMemberId(13);
        writer.AppendSingle(value.Single);

        writer.AppendMemberId(14);
        writer.AppendDouble(value.Double);

        writer.AppendMemberId(15);
        writer.AppendDecimal(value.Decimal);

        writer.AppendMemberId(16);
        writer.AppendString(value.String);

        writer.AppendMemberId(17);
        writer.AppendGuid(value.Guid);

        writer.AppendMemberId(18);
        writer.AppendDateOnly(value.DateOnly);

        writer.AppendMemberId(19);
        writer.AppendTimeOnly(value.TimeOnly);

        writer.AppendMemberId(20);
        writer.AppendTimeSpan(value.TimeSpan);

        writer.AppendMemberId(21);
        writer.AppendDateTimeOffset(value.DateTimeOffset);

        writer.AppendMemberId(22);
        writer.AppendNullMarker(value.NullableInt.HasValue);
        if (value.NullableInt.HasValue)
        {
            writer.AppendInt32(value.NullableInt.Value);
        }

        writer.AppendMemberId(23);
        writer.AppendNullMarker(value.NullableString is not null);
        if (value.NullableString is not null)
        {
            writer.AppendString(value.NullableString);
        }

        writer.AppendMemberId(24);
        writer.AppendCount(value.Array.Length);
        foreach (var element in value.Array)
        {
            writer.AppendInt32(element);
        }

        writer.AppendMemberId(25);
        writer.AppendCount(value.List.Count);
        foreach (var element in value.List)
        {
            writer.AppendInt32(element);
        }

        writer.AppendMemberId(26);
        writer.AppendCount(value.ReadOnlyList.Count);
        foreach (var element in value.ReadOnlyList)
        {
            writer.AppendInt32(element);
        }

        writer.AppendMemberId(27);
        writer.AppendCount(value.Immutable.IsDefault ? 0 : value.Immutable.Length);
        if (!value.Immutable.IsDefault)
        {
            foreach (var element in value.Immutable)
            {
                writer.AppendInt32(element);
            }
        }

        writer.AppendMemberId(28);
        // Nested contract: its own full header, then its own members (Position's).
        writer.AppendContractHeader("integration.position", 1);
        writer.AppendMemberId(1);
        writer.AppendInt32(value.NestedPosition.X);
        writer.AppendMemberId(2);
        writer.AppendInt32(value.NestedPosition.Y);

        writer.AppendMemberId(29);
        writer.AppendByte((byte)value.Rarity);

        return writer.Finish();
    }
}
