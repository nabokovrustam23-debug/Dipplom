using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BarbershopCrm.Infrastructure.Loyalty;

public sealed class LoyaltyDiscountResolver : ILoyaltyDiscountResolver
{
    private readonly AppDbContext _db;
    private readonly LoyaltyOptions _options;

    public LoyaltyDiscountResolver(AppDbContext db, IOptions<LoyaltyOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<DiscountResolution> ResolveDiscountAsync(
        int clientId,
        DateTime bookingDateTime,
        DateOnly? clientBirthDate,
        CancellationToken ct = default)
    {
        var candidates = new List<(decimal Percent, string Reason)>();

        // 1. Скидка по уровню лояльности (на основе завершённых визитов)
        var completedVisits = await _db.Bookings
            .Where(b => b.ClientId == clientId && b.Status == BookingStatus.Completed)
            .Join(_db.Visits, b => b.BookingId, v => v.BookingId, (b, v) => v)
            .CountAsync(ct);

        var tier = GetTierForVisits(completedVisits);
        if (tier.DiscountPercent > 0)
        {
            candidates.Add((tier.DiscountPercent, "Tier"));
        }

        // 2. Скидка на первое посещение
        if (completedVisits == 0 && _options.FirstVisitDiscountPercent > 0)
        {
            candidates.Add((_options.FirstVisitDiscountPercent, "FirstVisit"));
        }

        // 3. Скидка на день рождения
        if (clientBirthDate.HasValue && _options.BirthdayDiscountPercent > 0)
        {
            var bookingDate = DateOnly.FromDateTime(bookingDateTime);
            if (IsWithinBirthdayWindow(bookingDate, clientBirthDate.Value, _options.BirthdayWindowDays))
            {
                candidates.Add((_options.BirthdayDiscountPercent, "Birthday"));
            }
        }

        // Политика: выбираем максимальную скидку (BestSingle)
        if (candidates.Count == 0)
        {
            return DiscountResolution.None();
        }

        var best = candidates.OrderByDescending(c => c.Percent).First();
        return new DiscountResolution
        {
            DiscountPercent = best.Percent,
            Reason = best.Reason
        };
    }

    private LoyaltyTier GetTierForVisits(int visits)
    {
        var sortedTiers = _options.Tiers.OrderByDescending(t => t.MinVisits).ToList();
        return sortedTiers.FirstOrDefault(t => visits >= t.MinVisits)
               ?? sortedTiers.LastOrDefault()
               ?? new LoyaltyTier { Code = "Standard", MinVisits = 0, DiscountPercent = 0 };
    }

    private static bool IsWithinBirthdayWindow(DateOnly bookingDate, DateOnly birthDate, int windowDays)
    {
        // Переносим день рождения на текущий год
        var birthdayThisYear = new DateOnly(bookingDate.Year, birthDate.Month, birthDate.Day);
        
        // Обработка 29 февраля в невисокосный год
        if (birthDate.Month == 2 && birthDate.Day == 29 && !DateTime.IsLeapYear(bookingDate.Year))
        {
            birthdayThisYear = new DateOnly(bookingDate.Year, 2, 28);
        }

        // Проверяем, попадает ли дата записи в окно ±windowDays от дня рождения
        var daysDiff = Math.Abs(bookingDate.DayNumber - birthdayThisYear.DayNumber);
        
        // Учитываем переход через границу года
        var daysInYear = DateTime.IsLeapYear(bookingDate.Year) ? 366 : 365;
        daysDiff = Math.Min(daysDiff, daysInYear - daysDiff);
        
        return daysDiff <= windowDays;
    }
}

// Made with Bob
