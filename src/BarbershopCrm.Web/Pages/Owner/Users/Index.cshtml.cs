using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Web.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Owner.Users;

[AuthorizePage(RoleCode.Owner)]
public class IndexModel : AppPageModel
{
    private const string DefaultPosition = "Барбер";
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db, ICurrentUserAccessor currentUser) : base(currentUser)
    {
        _db = db;
    }

    public List<UserRow> Rows { get; private set; } = new();
    public List<RoleOption> AvailableRoles { get; private set; } = new();

    public sealed class UserRow
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string? BranchName { get; set; }
    }

    public sealed class RoleOption
    {
        public int RoleId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadDataAsync(ct);
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(int userId, int newRoleId, CancellationToken ct)
    {
        if (userId == Current!.UserId)
        {
            TempData["Error"] = "Нельзя изменить свою собственную роль.";
            return RedirectToPage("/Owner/Users/Index");
        }

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Persona)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user is null)
        {
            TempData["Error"] = "Пользователь не найден.";
            return RedirectToPage("/Owner/Users/Index");
        }

        if (user.Role.Code == RoleCode.Owner)
        {
            TempData["Error"] = "Нельзя изменить роль владельца.";
            return RedirectToPage("/Owner/Users/Index");
        }

        var newRole = await _db.Roles.FindAsync(new object[] { newRoleId }, ct);
        if (newRole is null || newRole.Code == RoleCode.Owner)
        {
            TempData["Error"] = "Недопустимая роль.";
            return RedirectToPage("/Owner/Users/Index");
        }

        if (newRole.Code is RoleCode.Master or RoleCode.Admin)
        {
            if (user.BranchId is null)
            {
                user.BranchId = await _db.Branches
                    .Where(b => b.IsActive)
                    .Select(b => (int?)b.BranchId)
                    .FirstOrDefaultAsync(ct);

                if (user.BranchId is null)
                {
                    TempData["Error"] = "Нет активных филиалов. Сначала создайте филиал.";
                    return RedirectToPage("/Owner/Users/Index");
                }
            }
        }

        if (newRole.Code == RoleCode.Master)
        {
            var existingMaster = await _db.Masters.FirstOrDefaultAsync(m => m.PersonaId == user.PersonaId, ct);
            if (existingMaster is null)
            {
                _db.Masters.Add(new Master
                {
                    PersonaId = user.PersonaId,
                    BranchId = user.BranchId!.Value,
                    Position = DefaultPosition,
                    HireDate = DateOnly.FromDateTime(DateTime.Now),
                    IsActive = true,
                });
            }
        }

        if (newRole.Code == RoleCode.Client)
        {
            var hasClient = await _db.Clients.AnyAsync(c => c.PersonaId == user.PersonaId, ct);
            if (!hasClient)
            {
                _db.Clients.Add(new Client
                {
                    PersonaId = user.PersonaId,
                    Source = "Изменение роли",
                    CreatedAt = DateTime.Now,
                });
            }
        }

        user.RoleId = newRoleId;
        await _db.SaveChangesAsync(ct);

        if (newRole.Code == RoleCode.Master)
        {
            var masterId = await _db.Masters
                .Where(m => m.PersonaId == user.PersonaId)
                .Select(m => m.MasterId)
                .FirstAsync(ct);

            TempData["Success"] = $"Пользователь «{user.Persona.LastName} {user.Persona.FirstName}» повышен до мастера. Назначьте услуги, филиал и укажите дополнительную информацию.";
            return RedirectToPage("/Masters/Edit", new { id = masterId });
        }

        TempData["Success"] = $"Роль пользователя «{user.Persona.LastName} {user.Persona.FirstName}» изменена на «{newRole.Name}».";
        return RedirectToPage("/Owner/Users/Index");
    }

    private async Task LoadDataAsync(CancellationToken ct)
    {
        var users = await _db.Users
            .Include(u => u.Persona)
            .Include(u => u.Role)
            .Include(u => u.Branch)
            .Where(u => u.Role.Code != RoleCode.Owner)
            .OrderBy(u => u.Role.RoleId)
            .ThenBy(u => u.Persona.LastName)
            .ThenBy(u => u.Persona.FirstName)
            .AsNoTracking()
            .ToListAsync(ct);

        Rows = users.Select(u => new UserRow
        {
            UserId = u.UserId,
            FullName = $"{u.Persona.LastName} {u.Persona.FirstName}",
            Login = u.Login,
            Phone = u.Persona.Phone,
            RoleName = u.Role.Name,
            RoleId = u.Role.RoleId,
            BranchName = u.Branch?.Name,
        }).ToList();

        AvailableRoles = await _db.Roles
            .Where(r => r.Code != RoleCode.Owner)
            .OrderBy(r => r.RoleId)
            .AsNoTracking()
            .Select(r => new RoleOption { RoleId = r.RoleId, Name = r.Name })
            .ToListAsync(ct);
    }
}
