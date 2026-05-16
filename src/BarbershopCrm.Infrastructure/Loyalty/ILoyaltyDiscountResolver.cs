namespace BarbershopCrm.Infrastructure.Loyalty;

public interface ILoyaltyDiscountResolver
{
    /// <summary>
    /// Рассчитать скидку для записи клиента
    /// </summary>
    Task<DiscountResolution> ResolveDiscountAsync(
        int clientId, 
        DateTime bookingDateTime, 
        DateOnly? clientBirthDate,
        CancellationToken ct = default);
}

public sealed class DiscountResolution
{
    public decimal DiscountPercent { get; set; }
    public string Reason { get; set; } = "None";
    
    public static DiscountResolution None() => new() { DiscountPercent = 0, Reason = "None" };
}

// Made with Bob
