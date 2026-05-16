using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Loyalty;
using BarbershopCrm.Web.Auth;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Web.Pages.Profile;

[AuthorizePage]
public sealed class IndexModel : AppPageModel
{
    private readonly AppDbContext _db;
    private readonly ILoyaltyService? _loyaltyService;

    public IndexModel(ICurrentUserAccessor currentUser, AppDbContext db, ILoyaltyService? loyaltyService = null)
        : base(currentUser)
    {
        _db = db;
        _loyaltyService = loyaltyService;
    }

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
        if (Current?.RoleCode == RoleCode.Client && Current.PersonaId > 0 && _loyaltyService != null)
        {
            var clientId = await _db.Clients
                .Where(c => c.PersonaId == Current.PersonaId)
                .Select(c => c.ClientId)
                .FirstOrDefaultAsync();

            if (clientId > 0)
            {
                LoyaltyInfo = await _loyaltyService.GetClientLoyaltyInfoAsync(clientId);
            }
        }
    }
}
