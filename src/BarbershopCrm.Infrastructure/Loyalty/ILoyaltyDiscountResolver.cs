namespace BarbershopCrm.Infrastructure.Loyalty;

public interface ILoyaltyDiscountResolver
{
    public const string ReasonTier = "Tier";
    public const string ReasonFirstVisit = "FirstVisit";
    public const string ReasonBirthday = "Birthday";
    public const string ReasonNone = "None";

    Task<DiscountResolution> ResolveDiscountAsync(
        int clientId,
        DateTime bookingDateTime,
        DateOnly? clientBirthDate,
        CancellationToken ct = default);
}

public sealed class DiscountResolution
{
    public decimal DiscountPercent { get; set; }
    public string Reason { get; set; } = ILoyaltyDiscountResolver.ReasonNone;

    public static DiscountResolution None() =>
        new() { DiscountPercent = 0, Reason = ILoyaltyDiscountResolver.ReasonNone };
}

// Made with Bob
