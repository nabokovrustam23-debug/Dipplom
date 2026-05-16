namespace BarbershopCrm.Infrastructure.Loyalty;

public sealed class LoyaltyOptions
{
    public List<LoyaltyTier> Tiers { get; set; } = new();
    public decimal FirstVisitDiscountPercent { get; set; }
    public decimal BirthdayDiscountPercent { get; set; }
    public int BirthdayWindowDays { get; set; }
    public string DiscountPolicy { get; set; } = "BestSingle";
}

public sealed class LoyaltyTier
{
    public string Code { get; set; } = null!;
    public int MinVisits { get; set; }
    public decimal DiscountPercent { get; set; }
}

// Made with Bob
