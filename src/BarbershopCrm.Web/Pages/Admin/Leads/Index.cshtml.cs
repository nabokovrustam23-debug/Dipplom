using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Admin.Leads;

[AuthorizePage(RoleCode.Admin, RoleCode.Owner)]
public class IndexModel : AppPageModel
{
    private readonly AppDbContext _db;

    public IndexModel(ICurrentUserAccessor cu, AppDbContext db) : base(cu)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? SortBy { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? DateTo { get; set; }
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public const int PageSize = 50;

    public List<Domain.Entities.Lead> Items { get; private set; } = new();
    public List<string> StatusOptions { get; } = LeadStatus.All.ToList();
    public int TotalCount { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Current is null) return Forbid();

        var q = _db.Leads.AsNoTracking()
            .Include(l => l.PreferredBranch)
            .AsQueryable();

        if (Current.RoleCode == RoleCode.Admin && Current.BranchId.HasValue)
        {
            var bid = Current.BranchId.Value;
            q = q.Where(l => l.PreferredBranchId == null || l.PreferredBranchId == bid);
        }

        if (!string.IsNullOrWhiteSpace(Status) && LeadStatus.All.Contains(Status))
            q = q.Where(l => l.Status == Status);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            q = q.Where(l => l.RawName.Contains(s) || l.RawPhone.Contains(s));
        }

        if (DateFrom.HasValue)
            q = q.Where(l => l.CreatedAt >= DateFrom.Value);

        if (DateTo.HasValue)
            q = q.Where(l => l.CreatedAt < DateTo.Value.AddDays(1));

        TotalCount = await q.CountAsync(ct);

        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

        q = (SortBy ?? "created_desc") switch
        {
            "created_asc" => q.OrderBy(l => l.CreatedAt),
            "name_asc" => q.OrderBy(l => l.RawName),
            _ => q.OrderByDescending(l => l.CreatedAt),
        };

        Items = await q.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostTakeAsync(int id, CancellationToken ct)
    {
        if (Current is null) return Forbid();
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.LeadId == id, ct);
        if (lead is null) return NotFound();
        if (lead.Status != LeadStatus.New)
        {
            TempData["Error"] = "Заявка уже не в статусе «Новая».";
            return RedirectToPage(new { Status, Search, SortBy, DateFrom, DateTo, CurrentPage });
        }
        lead.Status = LeadStatus.InProgress;
        lead.ProcessedByUserId = Current.UserId;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Заявка взята в работу.";
        return RedirectToPage(new { Status, Search, SortBy, DateFrom, DateTo, CurrentPage });
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, CancellationToken ct)
    {
        if (Current is null) return Forbid();
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.LeadId == id, ct);
        if (lead is null) return NotFound();
        if (lead.Status is LeadStatus.Done or LeadStatus.Rejected)
        {
            TempData["Error"] = "Заявка уже закрыта.";
            return RedirectToPage(new { Status, Search, SortBy, DateFrom, DateTo, CurrentPage });
        }
        lead.Status = LeadStatus.Rejected;
        lead.ProcessedByUserId = Current.UserId;
        lead.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Заявка отклонена.";
        return RedirectToPage(new { Status, Search, SortBy, DateFrom, DateTo, CurrentPage });
    }

    public async Task<IActionResult> OnPostDoneAsync(int id, CancellationToken ct)
    {
        if (Current is null) return Forbid();
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.LeadId == id, ct);
        if (lead is null) return NotFound();
        if (lead.Status is LeadStatus.Done or LeadStatus.Rejected)
        {
            TempData["Error"] = "Заявка уже закрыта.";
            return RedirectToPage(new { Status, Search, SortBy, DateFrom, DateTo, CurrentPage });
        }
        lead.Status = LeadStatus.Done;
        lead.ProcessedByUserId = Current.UserId;
        lead.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Заявка закрыта без записи.";
        return RedirectToPage(new { Status, Search, SortBy, DateFrom, DateTo, CurrentPage });
    }
}
