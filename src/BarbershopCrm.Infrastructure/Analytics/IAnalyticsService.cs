namespace BarbershopCrm.Infrastructure.Analytics;

public interface IAnalyticsService
{
    /// <summary>
    /// Aggregate KPIs for a branch (or the whole network if branchId is null) over [from, to].
    /// The interval is inclusive on both ends; internally translated to [from, to+1day) on StartDateTime.
    /// </summary>
    Task<DashboardSnapshot> GetDashboardAsync(int? branchId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>
    /// Comparison row per active branch over [from, to]. Used by /Owner/Analytics/Compare.
    /// </summary>
    Task<IReadOnlyList<BranchCompareRow>> GetBranchComparisonAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>
    /// Per-booking flat rows for CSV export over [from, to], optionally restricted to a single branch.
    /// </summary>
    Task<IReadOnlyList<BookingExportRow>> GetExportRowsAsync(int? branchId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>
    /// Получить сегментацию клиентов с ABC-анализом и распределением по уровням.
    /// Анализирует всех клиентов с завершенными визитами за указанный период.
    /// </summary>
    Task<ClientSegmentationSnapshot> GetClientSegmentationAsync(int? branchId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>
    /// Получить детальный список всех клиентов с аналитикой для экспорта или детального просмотра.
    /// </summary>
    Task<IReadOnlyList<ClientAnalyticsRow>> GetClientAnalyticsRowsAsync(int? branchId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
