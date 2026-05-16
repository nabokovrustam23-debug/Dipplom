namespace BarbershopCrm.Infrastructure.Loyalty;

public interface ILoyaltyService
{
    /// <summary>
    /// Получить информацию о лояльности клиента
    /// </summary>
    Task<ClientLoyaltyInfo> GetClientLoyaltyInfoAsync(int clientId, CancellationToken ct = default);

    /// <summary>
    /// Получить историю применения скидок клиента
    /// </summary>
    Task<ClientLoyaltyHistory> GetLoyaltyHistoryAsync(int clientId, CancellationToken ct = default);
}

public sealed class ClientLoyaltyInfo
{
    public int CompletedVisits { get; set; }
    public LoyaltyTierInfo CurrentTier { get; set; } = null!;
    public LoyaltyTierInfo? NextTier { get; set; }
    public int VisitsToNextTier { get; set; }
    public string MotivationText { get; set; } = string.Empty;
}

public sealed class LoyaltyTierInfo
{
    public string Code { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public decimal DiscountPercent { get; set; }
    public int MinVisits { get; set; }
}

public sealed class ClientLoyaltyHistory
{
    public List<LoyaltyHistoryRow> DiscountedBookings { get; set; } = new();
}

public sealed class LoyaltyHistoryRow
{
    public DateTime Date { get; set; }
    public string ServiceName { get; set; } = null!;
    public decimal BasePrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal FinalAmount { get; set; }
    public string DiscountReason { get; set; } = null!;
    public string BranchName { get; set; } = null!;
    public string? MasterName { get; set; }
}

// Made with Bob
