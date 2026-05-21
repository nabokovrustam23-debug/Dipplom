using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;

namespace BarbershopCrm.Infrastructure.Scheduling;

public sealed record TimelineHour(TimeOnly Time, int RowIndex);

public sealed record TimelineEntry(
    TimelineEntryKind Kind,
    int MasterId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int RowStart,
    int RowSpan,
    Booking? Booking,
    string? Label);

public enum TimelineEntryKind { Booking, Lunch, DayOff, Vacation, SickLeave }

public sealed record TimelineResult(
    TimeOnly TimelineStart,
    TimeOnly TimelineEnd,
    List<TimelineHour> TimelineHours,
    int TotalRows,
    List<TimelineEntry> Entries);

public interface ITimelineService
{
    TimelineResult Build(List<Booking> bookings, List<WorkSchedule> schedule);
}

public sealed class TimelineService : ITimelineService
{
    private const int MinutesPerRow = 15;

    public TimelineResult Build(List<Booking> bookings, List<WorkSchedule> schedule)
    {
        var workRanges = schedule
            .Where(w => string.Equals(w.ScheduleType, ScheduleType.Work, StringComparison.Ordinal))
            .ToList();

        TimeOnly start;
        TimeOnly end;

        if (workRanges.Count > 0)
        {
            start = workRanges.Min(w => w.StartTime);
            end = workRanges.Max(w => w.EndTime);
        }
        else
        {
            start = new TimeOnly(10, 0);
            end = new TimeOnly(20, 0);
        }

        foreach (var b in bookings)
        {
            var s = TimeOnly.FromDateTime(b.StartDateTime);
            var e = TimeOnly.FromDateTime(b.StartDateTime.AddMinutes(b.DurationMinutes));
            if (s < start) start = s;
            if (e > end) end = e;
        }

        start = new TimeOnly(start.Hour, 0);
        if (end.Minute > 0) end = new TimeOnly(Math.Min(23, end.Hour + 1), 0);

        var hours = new List<TimelineHour>();
        for (var h = start; h < end; h = h.AddHours(1))
        {
            var rowIndex = MinutesBetween(start, h) / MinutesPerRow;
            hours.Add(new TimelineHour(h, rowIndex + 1));
        }

        var totalRows = Math.Max(1, MinutesBetween(start, end) / MinutesPerRow);

        var entries = new List<TimelineEntry>();

        foreach (var b in bookings)
        {
            var s = TimeOnly.FromDateTime(b.StartDateTime);
            var e = TimeOnly.FromDateTime(b.StartDateTime.AddMinutes(b.DurationMinutes));
            entries.Add(new TimelineEntry(
                TimelineEntryKind.Booking,
                b.MasterId,
                s, e,
                RowFor(s, start), Math.Max(1, MinutesBetween(s, e) / MinutesPerRow),
                b, null));
        }

        foreach (var w in schedule)
        {
            TimelineEntryKind? kind = w.ScheduleType switch
            {
                "Lunch"     => TimelineEntryKind.Lunch,
                "DayOff"    => TimelineEntryKind.DayOff,
                "Vacation"  => TimelineEntryKind.Vacation,
                "SickLeave" => TimelineEntryKind.SickLeave,
                _ => null,
            };
            if (kind is null) continue;

            var label = kind switch
            {
                TimelineEntryKind.Lunch     => "Перерыв",
                TimelineEntryKind.DayOff    => "Выходной",
                TimelineEntryKind.Vacation  => "Отпуск",
                TimelineEntryKind.SickLeave => "Больничный",
                _ => string.Empty,
            };
            entries.Add(new TimelineEntry(
                kind.Value,
                w.MasterId,
                w.StartTime, w.EndTime,
                RowFor(w.StartTime, start),
                Math.Max(1, MinutesBetween(w.StartTime, w.EndTime) / MinutesPerRow),
                null, label));
        }

        entries = entries.OrderBy(e => e.StartTime).ToList();

        return new TimelineResult(start, end, hours, totalRows, entries);
    }

    private static int RowFor(TimeOnly t, TimeOnly timelineStart) =>
        1 + Math.Max(0, MinutesBetween(timelineStart, t) / MinutesPerRow);

    private static int MinutesBetween(TimeOnly a, TimeOnly b) =>
        (int)(b - a).TotalMinutes;
}
