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
            .CountAsync(b => b.ClientId == clientId && b.Status == BookingStatus.Completed && b.Visit != null, ct);

        var tier = _options.GetTierForVisits(completedVisits);
        if (tier.DiscountPercent > 0)
        {
            candidates.Add((tier.DiscountPercent, ILoyaltyDiscountResolver.ReasonTier));
        }

        // 2. Скидка на первое посещение
        if (completedVisits == 0 && _options.FirstVisitDiscountPercent > 0)
        {
            candidates.Add((_options.FirstVisitDiscountPercent, ILoyaltyDiscountResolver.ReasonFirstVisit));
        }

        // 3. Скидка на день рождения
        if (clientBirthDate.HasValue && _options.BirthdayDiscountPercent > 0)
        {
            var bookingDate = DateOnly.FromDateTime(bookingDateTime);
            if (IsWithinBirthdayWindow(bookingDate, clientBirthDate.Value, _options.BirthdayWindowDays))
            {
                candidates.Add((_options.BirthdayDiscountPercent, ILoyaltyDiscountResolver.ReasonBirthday));
            }
        }

        // Политика: выбираем максимальную скидку
        if (candidates.Count == 0)
        {
            return DiscountResolution.None();
        }

        return _options.DiscountPolicy switch
        {
            DiscountPolicyCode.Cumulative => new DiscountResolution
            {
                DiscountPercent = Math.Min(candidates.Sum(c => c.Percent), 100),
                Reason = string.Join(", ", candidates.Select(c => c.Reason))
            },
            DiscountPolicyCode.BestSingle or _ => candidates
                .OrderByDescending(c => c.Percent)
                .Select(c => new DiscountResolution { DiscountPercent = c.Percent, Reason = c.Reason })
                .First()
        };
    }

    private static bool IsWithinBirthdayWindow(DateOnly bookingDate, DateOnly birthDate, int windowDays)
    {
        var birthdayCandidates = new[]
        {
            GetBirthdayForYear(bookingDate.Year - 1, birthDate),
            GetBirthdayForYear(bookingDate.Year, birthDate),
            GetBirthdayForYear(bookingDate.Year + 1, birthDate)
        };

        return birthdayCandidates.Any(bday => Math.Abs(bookingDate.DayNumber - bday.DayNumber) <= windowDays);
    }

    private static DateOnly GetBirthdayForYear(int year, DateOnly birthDate)
    {
        if (birthDate.Month == 2 && birthDate.Day == 29 && !DateTime.IsLeapYear(year))
        {
            return new DateOnly(year, 2, 28);
        }

        return new DateOnly(year, birthDate.Month, birthDate.Day);
    }
}

// Made with Bob
