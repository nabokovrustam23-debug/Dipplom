using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Auth;
using BarbershopCrm.Infrastructure.Data;
using BarbershopCrm.Infrastructure.Loyalty;
using BarbershopCrm.Web.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BarbershopCrm.Web.Pages.Account.Loyalty;

[AuthorizePage(RoleCode.Client)]
public sealed class IndexModel : AppPageModel
{
    private readonly AppDbContext _db;
    private readonly ILoyaltyService _loyaltyService;
    private readonly LoyaltyOptions _loyaltyOptions;

    public IndexModel(
        ICurrentUserAccessor currentUser, 
        AppDbContext db, 
        ILoyaltyService loyaltyService,
        IOptions<LoyaltyOptions> loyaltyOptions)
        : base(currentUser)
    {
        _db = db;
        _loyaltyService = loyaltyService;
        _loyaltyOptions = loyaltyOptions.Value;
    }

    public ClientLoyaltyInfo LoyaltyInfo { get; set; } = null!;
    public ClientLoyaltyHistory LoyaltyHistory { get; set; } = null!;
    public List<LoyaltyTier> AllTiers { get; set; } = new();
    public DateOnly? BirthDate { get; set; }
    public decimal BirthdayDiscountPercent { get; set; }
    public int BirthdayWindowDays { get; set; }

    public async Task OnGetAsync()
    {
        if (Current?.PersonaId is null or <= 0)
        {
            return;
        }

        // Получаем ClientId
        var client = await _db.Clients
            .Include(c => c.Persona)
            .FirstOrDefaultAsync(c => c.PersonaId == Current.PersonaId);

        if (client == null)
        {
            return;
        }

        // Получаем информацию о лояльности
        LoyaltyInfo = await _loyaltyService.GetClientLoyaltyInfoAsync(client.ClientId);
        
        // Получаем историю скидок
        LoyaltyHistory = await _loyaltyService.GetLoyaltyHistoryAsync(client.ClientId);

        // Получаем все уровни из конфигурации
        AllTiers = _loyaltyOptions.Tiers.OrderBy(t => t.MinVisits).ToList();

        // Информация о дне рождения
        BirthDate = client.Persona.BirthDate;
        BirthdayDiscountPercent = _loyaltyOptions.BirthdayDiscountPercent;
        BirthdayWindowDays = _loyaltyOptions.BirthdayWindowDays;
    }
}

// Made with Bob
