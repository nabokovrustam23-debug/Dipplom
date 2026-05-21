namespace BarbershopCrm.Infrastructure.Loyalty;

public enum DiscountPolicyCode
{
    BestSingle,
    Cumulative
}

public sealed class LoyaltyOptions
{
    public List<LoyaltyTier> Tiers { get; set; } = new();
    public decimal FirstVisitDiscountPercent { get; set; }
    public decimal BirthdayDiscountPercent { get; set; }
    public int BirthdayWindowDays { get; set; }
    public DiscountPolicyCode DiscountPolicy { get; set; } = DiscountPolicyCode.BestSingle;

    public LoyaltyTier GetTierForVisits(int visits)
    {
        var sortedTiers = Tiers.OrderByDescending(t => t.MinVisits).ToList();
        return sortedTiers.FirstOrDefault(t => visits >= t.MinVisits)
               ?? sortedTiers.LastOrDefault()
               ?? new LoyaltyTier { Code = "Standard", MinVisits = 0, DiscountPercent = 0 };
    }

    public LoyaltyTier? GetNextTier(int currentVisits)
    {
        return Tiers
            .Where(t => t.MinVisits > currentVisits)
            .OrderBy(t => t.MinVisits)
            .FirstOrDefault();
    }
}

public sealed class LoyaltyTier
{
    public string Code { get; set; } = null!;
    public int MinVisits { get; set; }
    public decimal DiscountPercent { get; set; }
}

// Made with Bob
