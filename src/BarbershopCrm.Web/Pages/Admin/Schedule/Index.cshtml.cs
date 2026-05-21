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

namespace BarbershopCrm.Web.Pages.Admin.Schedule;

[AuthorizePage(RoleCode.Admin, RoleCode.Owner)]
public class IndexModel : AppPageModel
{
    private readonly AppDbContext _db;
    private readonly IBookingService _service;
    private readonly ITimelineService _timeline;

    public IndexModel(AppDbContext db, ICurrentUserAccessor currentUser, IBookingService service, ITimelineService timeline) : base(currentUser)
    {
        _db = db;
        _service = service;
        _timeline = timeline;
    }

    [BindProperty(SupportsGet = true)]
    public int? BranchId { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    [BindProperty] public CompleteInput Complete { get; set; } = new();

    public IList<Branch> Branches { get; private set; } = Array.Empty<Branch>();
    public Branch? CurrentBranch { get; private set; }
    public DateOnly DateValue { get; private set; }

    public List<Master> Masters { get; private set; } = new();
    public TimeOnly TimelineStart { get; private set; }
    public TimeOnly TimelineEnd { get; private set; }
    public List<TimelineHour> TimelineHours { get; private set; } = new();
    public Dictionary<int, List<TimelineEntry>> EntriesByMaster { get; private set; } = new();

    public ScheduleAgendaViewModel? Agenda { get; private set; }

    public sealed class CompleteInput
    {
        public int BookingId { get; set; }

        [StringLength(500, ErrorMessage = "Комментарий слишком длинный (макс. 500 символов).")]
        public string? MasterNotes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadPageDataAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id, CancellationToken ct)
    {
        if (Current is null) return Forbid();
        SyncRouteContextForPost();
        var r = await _service.ConfirmAsync(id, Current.UserId, Current.RoleCode, ct);
        TempData[r.Success ? "Success" : "Error"] = r.Success ? "Запись подтверждена." : (r.Message ?? "Ошибка");
        return RedirectToSchedule();
    }

    public async Task<IActionResult> OnPostNoShowAsync(int id, CancellationToken ct)
    {
        if (Current is null) return Forbid();
        SyncRouteContextForPost();
        var r = await _service.NoShowAsync(id, Current.UserId, Current.RoleCode, ct);
        TempData[r.Success ? "Success" : "Error"] = r.Success ? "Отмечено «не пришёл»." : (r.Message ?? "Ошибка");
        return RedirectToSchedule();
    }

    public async Task<IActionResult> OnPostCompleteAsync(CancellationToken ct)
    {
        if (Current is null) return Forbid();
        SyncRouteContextForPost();
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            TempData["Error"] = $"Ошибка валидации данных: {errors}";
            await LoadPageDataAsync(ct);
            return Page();
        }

        var r = await _service.CompleteAsync(
            new CompleteBookingCommand(Complete.BookingId, Complete.MasterNotes),
            Current.UserId, Current.RoleCode, ct);
        TempData[r.Success ? "Success" : "Error"] = r.Success ? "Визит зафиксирован." : (r.Message ?? "Ошибка");
        return RedirectToSchedule();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, string? reason, CancellationToken ct)
    {
        if (Current is null) return Forbid();
        SyncRouteContextForPost();
        if (Current.RoleCode == RoleCode.Admin)
        {
            var ownsBranch = await _db.Bookings.AsNoTracking()
                .AnyAsync(b => b.BookingId == id && b.BranchId == Current.BranchId, ct);
            if (!ownsBranch) return NotFound();
        }

        var r = await _service.CancelAsync(id, Current.UserId, Current.RoleCode, reason, ct);
        TempData[r.Success ? "Success" : "Error"] = r.Success ? "Запись отменена." : (r.Message ?? "Ошибка отмены.");
        return RedirectToSchedule();
    }

    private void SyncRouteContextForPost()
    {
        // Читаем BranchId из формы, если не установлен
        if (!BranchId.HasValue && Request.Form.ContainsKey("BranchId"))
        {
            if (int.TryParse(Request.Form["BranchId"], out var branchId))
                BranchId = branchId;
        }

        // Читаем Date из формы, если не установлен
        if (string.IsNullOrWhiteSpace(Date) && Request.Form.ContainsKey("Date"))
            Date = Request.Form["Date"];

        if (Current?.RoleCode == RoleCode.Admin && Current.BranchId.HasValue && !BranchId.HasValue)
            BranchId = Current.BranchId.Value;

        DateValue = !string.IsNullOrWhiteSpace(Date) && DateOnly.TryParse(Date, out var d)
            ? d
            : DateOnly.FromDateTime(DateTime.Today);
    }

    private IActionResult RedirectToSchedule()
    {
        var branchId = BranchId ?? Current?.BranchId;
        return RedirectToPage(new { BranchId = branchId, Date = DateValue.ToString("yyyy-MM-dd") });
    }

    private async Task LoadPageDataAsync(CancellationToken ct)
    {
        Agenda = null;
        DateValue = !string.IsNullOrWhiteSpace(Date) && DateOnly.TryParse(Date, out var d)
            ? d : DateOnly.FromDateTime(DateTime.Today);

        Branches = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(ct);

        if (Current?.RoleCode == RoleCode.Admin && Current.BranchId.HasValue)
        {
            BranchId = Current.BranchId.Value;
        }
        else if (BranchId is null && Branches.Count > 0)
        {
            BranchId = Branches[0].BranchId;
        }

        if (BranchId is null) return;

        CurrentBranch = Branches.FirstOrDefault(b => b.BranchId == BranchId.Value);

        Masters = await _db.Masters.AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive && m.BranchId == BranchId.Value)
            .OrderBy(m => m.Persona.LastName)
            .ToListAsync(ct);

        if (Masters.Count == 0) return;

        var masterIds = Masters.Select(m => m.MasterId).ToList();
        var dayStart = new DateTime(DateValue.Year, DateValue.Month, DateValue.Day);
        var dayEnd = dayStart.AddDays(1);

        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => masterIds.Contains(b.MasterId)
                        && b.StartDateTime >= dayStart && b.StartDateTime < dayEnd
                        && b.Status != BookingStatus.Cancelled)
            .Include(b => b.Service)
            .Include(b => b.Client).ThenInclude(c => c.Persona)
            .OrderBy(b => b.StartDateTime)
            .ToListAsync(ct);

        var schedule = await _db.WorkSchedules.AsNoTracking()
            .Where(w => masterIds.Contains(w.MasterId) && w.WorkDate == DateValue)
            .OrderBy(w => w.StartTime)
            .ToListAsync(ct);

        var result = _timeline.Build(bookings, schedule);
        TimelineStart = result.TimelineStart;
        TimelineEnd = result.TimelineEnd;
        TimelineHours = result.TimelineHours;
        EntriesByMaster = Masters.ToDictionary(m => m.MasterId, m =>
            result.Entries.Where(e => e.MasterId == m.MasterId)
                .OrderBy(e => e.StartTime).ToList());

        BuildAgenda();
    }

    private void BuildAgenda()
    {
        var sections = new List<ScheduleAgendaMasterSection>();
        foreach (var m in Masters)
        {
            var items = new List<ScheduleAgendaItem>();
            if (EntriesByMaster.TryGetValue(m.MasterId, out var list))
            {
                foreach (var e in list)
                {
                    if (e.Kind == TimelineEntryKind.Booking && e.Booking is not null)
                        items.Add(new ScheduleAgendaBookingItem(e.Booking, StatusLabels.BookingStatus(e.Booking.Status)));
                    else if (e.Kind != TimelineEntryKind.Booking && !string.IsNullOrEmpty(e.Label))
                        items.Add(new ScheduleAgendaBreakItem(e.Label, e.StartTime, e.EndTime, e.Kind.ToString().ToLowerInvariant()));
                }
            }

            var initial = m.Persona.LastName.Length > 0 ? m.Persona.LastName[0].ToString() : "?";
            sections.Add(new ScheduleAgendaMasterSection(m.Persona.ShortName, initial, items));
        }

        Agenda = new ScheduleAgendaViewModel
        {
            FormTarget = ScheduleAgendaFormTarget.AdminSchedule,
            DateParam = DateValue.ToString("yyyy-MM-dd"),
            BranchId = BranchId,
            Masters = sections,
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

    public static string Label(string type) => type switch
    {
        ScheduleType.Work => "Смена",
        ScheduleType.Lunch => "Обед",
        ScheduleType.DayOff => "Выходной",
        ScheduleType.Vacation => "Отпуск",
        ScheduleType.SickLeave => "Больничный",
        _ => type,
    };
}
