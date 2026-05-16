namespace BarbershopCrm.Infrastructure.Analytics;

public sealed record SalesFunnelSnapshot(
    int LeadsSubmittedInPeriod,
    int BookingsFromLeadsInPeriod,
    int CompletedBookingsFromLeadsInPeriod);

/// <summary>
/// Aggregated KPIs for a single branch (or the whole network if BranchId is null)
/// over the closed-open interval [From, To+1d).
/// </summary>
public sealed record DashboardSnapshot(
    DateOnly From,
    DateOnly To,
    int? BranchId,
    string? BranchName,
    BookingsByStatus ByStatus,
    int TotalBookings,
    decimal Revenue,
    decimal AverageTicket,
    int CompletedVisits,
    int RepeatClients,
    int ClientsWithAtLeastOneCompletedVisit,
    decimal RepeatClientsRate,
    IReadOnlyList<MasterUtilizationRow> Utilization,
    IReadOnlyList<TopServiceRow> TopServices,
    SalesFunnelSnapshot SalesFunnel);

public sealed record BookingsByStatus(
    int Created,
    int Confirmed,
    int Completed,
    int Cancelled,
    int NoShow)
{
    public int Total => Created + Confirmed + Completed + Cancelled + NoShow;
}

public sealed record MasterUtilizationRow(
    int MasterId,
    string MasterName,
    int BookedMinutes,
    int WorkMinutes,
    decimal UtilizationPercent);

public sealed record TopServiceRow(
    int ServiceId,
    string ServiceName,
    int Count,
    decimal Revenue);

public sealed record BranchCompareRow(
    int BranchId,
    string BranchName,
    int TotalBookings,
    int CompletedBookings,
    decimal Revenue,
    decimal AverageTicket,
    decimal AverageUtilizationPercent,
    decimal CancelRate,
    decimal NoShowRate);

/// <summary>
/// Flat row used for CSV export. One row per booking.
/// </summary>
public sealed record BookingExportRow(
    int BookingId,
    string BranchName,
    string MasterName,
    string ServiceName,
    string ClientName,
    DateTime StartDateTime,
    int DurationMinutes,
    decimal PriceSnapshot,
    string Status,
    string Source,
    decimal? VisitTotalAmount,
    string? CancelReason);

/// <summary>
/// ABC-категория клиента на основе выручки
/// </summary>
public enum ClientAbcCategory
{
    A, // Топ 20% клиентов по выручке (обычно дают 80% дохода)
    B, // Средние 30% клиентов
    C  // Остальные 50% клиентов
}

/// <summary>
/// Уровень клиента на основе количества визитов
/// </summary>
public enum ClientTier
{
    New,      // 1 визит
    Regular,  // 2-5 визитов
    Loyal,    // 6-10 визитов
    VIP       // 11+ визитов
}

/// <summary>
/// Детальная информация о клиенте для аналитики
/// </summary>
public sealed record ClientAnalyticsRow(
    int ClientId,
    string ClientName,
    string? Phone,
    string? Email,
    int TotalVisits,
    decimal TotalRevenue,
    decimal AverageTicket,
    DateTime FirstVisitDate,
    DateTime LastVisitDate,
    int DaysSinceLastVisit,
    ClientAbcCategory AbcCategory,
    ClientTier Tier,
    string? PreferredMaster,
    string? PreferredService,
    string? Source);

/// <summary>
/// Сводка по сегментации клиентов
/// </summary>
public sealed record ClientSegmentationSnapshot(
    DateOnly From,
    DateOnly To,
    int? BranchId,
    string? BranchName,
    int TotalClients,
    AbcDistribution AbcDistribution,
    TierDistribution TierDistribution,
    IReadOnlyList<ClientAnalyticsRow> TopClients);

/// <summary>
/// Распределение клиентов по ABC-категориям
/// </summary>
public sealed record AbcDistribution(
    int CategoryACount,
    decimal CategoryARevenue,
    decimal CategoryARevenuePercent,
    int CategoryBCount,
    decimal CategoryBRevenue,
    decimal CategoryBRevenuePercent,
    int CategoryCCount,
    decimal CategoryCRevenue,
    decimal CategoryCRevenuePercent);

/// <summary>
/// Распределение клиентов по уровням
/// </summary>
public sealed record TierDistribution(
    int NewCount,
    int RegularCount,
    int LoyalCount,
    int VipCount);
