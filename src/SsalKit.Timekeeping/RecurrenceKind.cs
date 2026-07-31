namespace SsalKit.Timekeeping;

/// <summary>
/// The calendar cadence a <see cref="RecurrenceSchedule"/> repeats on. Internal on purpose: the
/// cadence is selected through the factory methods (<see cref="RecurrenceSchedule.Daily"/>,
/// <see cref="RecurrenceSchedule.Weekly"/>, <see cref="RecurrenceSchedule.Monthly"/>), each of
/// which takes exactly the parameters its cadence needs, so no public enum can be passed with
/// parameters that do not apply to it.
/// </summary>
internal enum RecurrenceKind
{
    /// <summary>Every calendar day.</summary>
    Daily,

    /// <summary>Every calendar week, on one fixed day of the week.</summary>
    Weekly,

    /// <summary>Every calendar month, on one fixed day of the month.</summary>
    Monthly,
}
