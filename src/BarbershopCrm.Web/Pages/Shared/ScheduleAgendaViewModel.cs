using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;

namespace BarbershopCrm.Web.Pages.Shared;

public enum ScheduleAgendaFormTarget
{
    AdminSchedule,
    MasterBookings,
}

public static class ScheduleAgendaRules
{
    public static bool CanConfirm(Booking b) =>
        b.Status == BookingStatus.Created;

    public static bool CanCancel(Booking b) =>
        b.Status is BookingStatus.Created or BookingStatus.Confirmed;

    /// <summary>Завершение и «не пришёл» — только с момента начала записи (UTC).</summary>
    public static bool CanCompleteOrNoShow(Booking b) =>
        (b.Status is BookingStatus.Created or BookingStatus.Confirmed)
        && DateTime.UtcNow >= b.StartDateTime;
}

public sealed class ScheduleAgendaViewModel
{
    public required ScheduleAgendaFormTarget FormTarget { get; init; }
    public required string DateParam { get; init; }
    public int? BranchId { get; init; }
    public required IReadOnlyList<ScheduleAgendaMasterSection> Masters { get; init; }
}

public sealed class ScheduleAgendaMasterSection
{
    public ScheduleAgendaMasterSection(string name, string initial, IReadOnlyList<ScheduleAgendaItem> items)
    {
        Name = name;
        Initial = initial;
        Items = items;
    }

    public string Name { get; }
    public string Initial { get; }
    public IReadOnlyList<ScheduleAgendaItem> Items { get; }
}

public abstract class ScheduleAgendaItem;

public sealed class ScheduleAgendaBookingItem : ScheduleAgendaItem
{
    public ScheduleAgendaBookingItem(Booking booking, string statusLabel)
    {
        Booking = booking;
        StatusLabel = statusLabel;
    }

    public Booking Booking { get; }
    public string StatusLabel { get; }
}

public sealed class ScheduleAgendaBreakItem : ScheduleAgendaItem
{
    public ScheduleAgendaBreakItem(string label, TimeOnly start, TimeOnly end, string modifierCss)
    {
        Label = label;
        Start = start;
        End = end;
        ModifierCss = modifierCss;
    }

    public string Label { get; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }
    public string ModifierCss { get; }
}
