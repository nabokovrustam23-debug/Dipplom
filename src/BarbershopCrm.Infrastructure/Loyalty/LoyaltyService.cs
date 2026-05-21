using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BarbershopCrm.Infrastructure.Loyalty;

public sealed class LoyaltyService : ILoyaltyService
{
    private readonly AppDbContext _db;
    private readonly LoyaltyOptions _options;

    public LoyaltyService(AppDbContext db, IOptions<LoyaltyOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<ClientLoyaltyInfo> GetClientLoyaltyInfoAsync(int clientId, CancellationToken ct = default)
    {
        // Подсчитываем завершённые визиты клиента
        var completedVisits = await _db.Bookings
            .CountAsync(b => b.ClientId == clientId && b.Status == BookingStatus.Completed && b.Visit != null, ct);

        // Определяем текущий уровень
        var currentTier = _options.GetTierForVisits(completedVisits);
        var currentTierInfo = new LoyaltyTierInfo
        {
            Code = currentTier.Code,
            DisplayName = GetTierDisplayName(currentTier.Code),
            DiscountPercent = currentTier.DiscountPercent,
            MinVisits = currentTier.MinVisits
        };

        // Определяем следующий уровень
        var nextTier = _options.GetNextTier(completedVisits);
        LoyaltyTierInfo? nextTierInfo = null;
        var visitsToNext = 0;
        var motivationText = string.Empty;

        if (nextTier != null)
        {
            nextTierInfo = new LoyaltyTierInfo
            {
                Code = nextTier.Code,
                DisplayName = GetTierDisplayName(nextTier.Code),
                DiscountPercent = nextTier.DiscountPercent,
                MinVisits = nextTier.MinVisits
            };
            visitsToNext = nextTier.MinVisits - completedVisits;
            motivationText = $"До уровня {nextTierInfo.DisplayName} осталось {visitsToNext} {GetVisitsWord(visitsToNext)}. " +
                           $"Скидка увеличится до {nextTier.DiscountPercent}%!";
        }
        else
        {
            motivationText = $"Вы достигли максимального уровня {currentTierInfo.DisplayName}! " +
                           $"Ваша постоянная скидка: {currentTier.DiscountPercent}%.";
        }

        return new ClientLoyaltyInfo
        {
            CompletedVisits = completedVisits,
            CurrentTier = currentTierInfo,
            NextTier = nextTierInfo,
            VisitsToNextTier = visitsToNext,
            MotivationText = motivationText
        };
    }

    public async Task<ClientLoyaltyHistory> GetLoyaltyHistoryAsync(int clientId, CancellationToken ct = default)
    {
        // Получаем все завершённые бронирования клиента с примененными скидками
        var discountedBookings = await _db.Bookings
            .Where(b => b.ClientId == clientId
                     && b.Status == BookingStatus.Completed
                     && b.LoyaltyDiscountPercent > 0)
            .Include(b => b.Service)
            .Include(b => b.Branch)
            .Include(b => b.Master)
                .ThenInclude(m => m.Persona)
            .Include(b => b.Visit)
            .OrderByDescending(b => b.StartDateTime)
            .Select(b => new LoyaltyHistoryRow
            {
                Date = b.Visit != null ? b.Visit.CompletedAt : b.StartDateTime,
                ServiceName = b.Service.Name,
                BasePrice = b.PriceSnapshot,
                DiscountPercent = b.LoyaltyDiscountPercent,
                FinalAmount = b.Visit != null ? b.Visit.TotalAmount : b.PriceSnapshot * (1 - b.LoyaltyDiscountPercent / 100),
                DiscountReason = GetDiscountReasonDisplayName(b.LoyaltyDiscountReason),
                BranchName = b.Branch.Name,
                MasterName = b.Master.Persona.FullName
            })
            .ToListAsync(ct);

        return new ClientLoyaltyHistory
        {
            DiscountedBookings = discountedBookings
        };
    }

    private static string GetDiscountReasonDisplayName(string reason) => reason switch
    {
        ILoyaltyDiscountResolver.ReasonTier => "Уровень лояльности",
        ILoyaltyDiscountResolver.ReasonBirthday => "День рождения",
        ILoyaltyDiscountResolver.ReasonFirstVisit => "Первый визит",
        _ => "Скидка"
    };

    private static string GetTierDisplayName(string code) => code switch
    {
        "Standard" => "Стандарт",
        "VIP" => "VIP",
        "Premium" => "Премиум",
        _ => code
    };

    private static string GetVisitsWord(int count)
    {
        var lastDigit = count % 10;
        var lastTwoDigits = count % 100;

        if (lastTwoDigits >= 11 && lastTwoDigits <= 19)
            return "визитов";

        return lastDigit switch
        {
            1 => "визит",
            2 or 3 or 4 => "визита",
            _ => "визитов"
        };
    }
}

// Made with Bob
