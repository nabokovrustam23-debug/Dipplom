using System.ComponentModel.DataAnnotations;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Loyalty;
using BarbershopCrm.Infrastructure.Security;
using BarbershopCrm.Web.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Profile;

[AuthorizePage]
public sealed class IndexModel : AppPageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ILoyaltyService? _loyaltyService;

    public IndexModel(ICurrentUserAccessor currentUser, AppDbContext db, IPasswordHasher hasher, ILoyaltyService? loyaltyService = null)
        : base(currentUser)
    {
        _db = db;
        _hasher = hasher;
        _loyaltyService = loyaltyService;
    }

    [BindProperty]
    public ChangePasswordInput PasswordInput { get; set; } = new();

    public string RoleLabel => Current?.RoleCode switch
    {
        RoleCode.Owner  => "Владелец сети",
        RoleCode.Admin  => "Администратор филиала",
        RoleCode.Master => "Мастер",
        RoleCode.Client => "Клиент",
        _ => Current?.RoleCode ?? string.Empty,
    };

    public ClientLoyaltyInfo? LoyaltyInfo { get; set; }

    public async Task OnGetAsync()
    {
        await LoadLoyaltyAsync();
    }

    private async Task LoadLoyaltyAsync()
    {
        if (Current?.RoleCode != RoleCode.Client || Current.PersonaId <= 0 || _loyaltyService == null)
            return;

        var clientId = await _db.Clients
            .Where(c => c.PersonaId == Current.PersonaId)
            .Select(c => c.ClientId)
            .FirstOrDefaultAsync();

        if (clientId > 0)
        {
            LoyaltyInfo = await _loyaltyService.GetClientLoyaltyInfoAsync(clientId);
        }
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadLoyaltyAsync();
            return Page();
        }

        var user = await _db.Users.FindAsync(new object[] { Current!.UserId }, ct);
        if (user is null)
            return Forbid();

        if (!_hasher.Verify(PasswordInput.CurrentPassword, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            ModelState.AddModelError("PasswordInput.CurrentPassword", "Неверный текущий пароль.");
            return Page();
        }

        var hash = _hasher.Hash(PasswordInput.NewPassword);
        user.PasswordHash = hash.HashBase64;
        user.PasswordSalt = hash.SaltBase64;
        user.PasswordIterations = hash.Iterations;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "Пароль успешно изменён.";
        return RedirectToPage();
    }

    public sealed class ChangePasswordInput
    {
        [Required(ErrorMessage = "Введите текущий пароль.")]
        [DataType(DataType.Password)]
        [Display(Name = "Текущий пароль")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите новый пароль.")]
        [StringLength(200, MinimumLength = 8, ErrorMessage = "Пароль должен быть не короче 8 символов.")]
        [DataType(DataType.Password)]
        [Display(Name = "Новый пароль")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Повторите новый пароль.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Пароли не совпадают.")]
        [Display(Name = "Повторите новый пароль")]
        public string NewPasswordRepeat { get; set; } = string.Empty;
    }
}
