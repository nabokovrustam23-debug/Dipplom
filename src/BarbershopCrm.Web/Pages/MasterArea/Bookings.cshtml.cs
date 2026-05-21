using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Bookings;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Scheduling;
using BarbershopCrm.Web.Auth;
using BarbershopCrm.Web.Pages.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BarbershopCrm.Web.Pages.MasterArea;

[AuthorizePage(RoleCode.Master)]
public class BookingsModel : AppPageModel
{
    private readonly AppDbContext _db;
    private readonly IBookingService _service;
    private readonly ITimelineService _timeline;

    public BookingsModel(ICurrentUserAccessor cu, AppDbContext db, IBookingService service, ITimelineService timeline) : base(cu)
    {
        _db = db;
        _service = service;
        _timeline = timeline;
    }

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    public DateOnly DateValue { get; private set; }
    public Master? Self { get; private set; }
    public Branch? Branch { get; private set; }

    public List<Booking> Bookings { get; private set; } = new();
    public List<WorkSchedule> DaySchedule { get; private set; } = new();

    // Параметры таймлайна
    public TimeOnly TimelineStart { get; private set; }
    public TimeOnly TimelineEnd { get; private set; }
    public List<TimelineHour> TimelineHours { get; private set; } = new();
    public List<TimelineEntry> TimelineEntries { get; private set; } = new();

    public ScheduleAgendaViewModel? Agenda { get; private set; }

    [BindProperty] public CompleteInput Complete { get; set; } = new();

    public sealed class CompleteInput
    {
        public int BookingId { get; set; }

        [StringLength(500, ErrorMessage = "Комментарий слишком длинный (макс. 500 символов).")]
        public string? MasterNotes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Current is null) return Forbid();
        await LoadAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id, CancellationToken ct)
    {
        if (Current is null) return Forbid();
        var r = await _service.ConfirmAsync(id, Current.UserId, Current.RoleCode, ct);
        TempData[r.Success ? "Success" : "Error"] = r.Success ? "Запись подтверждена." : (r.Message ?? "Ошибка");
        return RedirectToPage("/MasterArea/Bookings", new { Date });
    }

    public async Task<IActionResult> OnPostNoShowAsync(int id, CancellationToken ct)
    {
        if (Current is null) return Forbid();
        var r = await _service.NoShowAsync(id, Current.UserId, Current.RoleCode, ct);
        TempData[r.Success ? "Success" : "Error"] = r.Success ? "Отмечено «не пришёл»." : (r.Message ?? "Ошибка");
        return RedirectToPage("/MasterArea/Bookings", new { Date });
    }

    public async Task<IActionResult> OnPostCompleteAsync(CancellationToken ct)
    {
        if (Current is null) return Forbid();
        if (!ModelState.IsValid)
        {
            await LoadAsync(ct);
            return Page();
        }
        var r = await _service.CompleteAsync(
            new CompleteBookingCommand(Complete.BookingId, Complete.MasterNotes),
            Current.UserId, Current.RoleCode, ct);
        TempData[r.Success ? "Success" : "Error"] = r.Success ? "Визит зафиксирован." : (r.Message ?? "Ошибка");
        return RedirectToPage("/MasterArea/Bookings", new { Date });
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Agenda = null;
        DateValue = !string.IsNullOrWhiteSpace(Date) && DateOnly.TryParse(Date, out var d)
            ? d : DateOnly.FromDateTime(DateTime.Today);

        Self = await _db.Masters.AsNoTracking()
            .Include(m => m.Persona)
            .Include(m => m.Branch)
            .FirstOrDefaultAsync(m => m.PersonaId == Current!.PersonaId, ct);

        if (Self is null) return;
        Branch = Self.Branch;

        var dayStart = new DateTime(DateValue.Year, DateValue.Month, DateValue.Day);
        var dayEnd = dayStart.AddDays(1);

        Bookings = await _db.Bookings.AsNoTracking()
            .Where(b => b.MasterId == Self.MasterId
                        && b.StartDateTime >= dayStart && b.StartDateTime < dayEnd
                        && b.Status != BookingStatus.Cancelled)
            .Include(b => b.Service)
            .Include(b => b.Client).ThenInclude(c => c.Persona)
            .Include(b => b.Branch)
            .OrderBy(b => b.StartDateTime)
            .ToListAsync(ct);

        DaySchedule = await _db.WorkSchedules.AsNoTracking()
            .Where(w => w.MasterId == Self.MasterId && w.WorkDate == DateValue)
            .OrderBy(w => w.StartTime)
            .ToListAsync(ct);

        var result = _timeline.Build(Bookings, DaySchedule);
        TimelineStart = result.TimelineStart;
        TimelineEnd = result.TimelineEnd;
        TimelineHours = result.TimelineHours;
        TimelineEntries = result.Entries;
        BuildAgenda();
    }

    private void BuildAgenda()
    {
        if (Self is null)
        {
            Agenda = null;
            return;
        }

        var items = new List<ScheduleAgendaItem>();
        foreach (var e in TimelineEntries)
        {
            if (e.Kind == TimelineEntryKind.Booking && e.Booking is not null)
                items.Add(new ScheduleAgendaBookingItem(e.Booking, StatusLabels.BookingStatus(e.Booking.Status)));
            else if (e.Kind != TimelineEntryKind.Booking && !string.IsNullOrEmpty(e.Label))
                items.Add(new ScheduleAgendaBreakItem(e.Label, e.StartTime, e.EndTime, e.Kind.ToString().ToLowerInvariant()));
        }

        var initial = Self.Persona.LastName.Length > 0 ? Self.Persona.LastName[0].ToString() : "?";
        Agenda = new ScheduleAgendaViewModel
        {
            FormTarget = ScheduleAgendaFormTarget.MasterBookings,
            DateParam = DateValue.ToString("yyyy-MM-dd"),
            BranchId = null,
            Masters = new List<ScheduleAgendaMasterSection> { new(Self.Persona.ShortName, initial, items) },
        };
    }

    public static string FormatRussianDate(DateOnly d)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var prefix = d == today ? "Сегодня" :
                     d == today.AddDays(1) ? "Завтра" :
                     d == today.AddDays(-1) ? "Вчера" :
                     culture.DateTimeFormat.GetDayName(d.DayOfWeek);
        prefix = char.ToUpperInvariant(prefix[0]) + prefix[1..];
        return $"{prefix}, {d.ToString("d MMMM yyyy", culture)}";
    }

    public static string FormatDuration(int totalMinutes)
    {
        var h = totalMinutes / 60;
        var m = totalMinutes % 60;
        if (h == 0) return $"{m} мин";
        if (m == 0) return $"{h} ч";
        return $"{h} ч {m} мин";
    }
}
