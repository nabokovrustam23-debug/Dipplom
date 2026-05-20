using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BarbershopCrm.Infrastructure.Analytics;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSnapshot> GetDashboardAsync(
        int? branchId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) (from, to) = (to, from);

        var fromDt = ToStart(from);
        var toDt = ToEndExclusive(to);

        // Bookings in scope (filtered by Start time and optional branch).
        var bookingsQ = _db.Bookings.AsNoTracking()
            .Where(b => b.StartDateTime >= fromDt && b.StartDateTime < toDt);
        if (branchId.HasValue)
            bookingsQ = bookingsQ.Where(b => b.BranchId == branchId.Value);

        // 1. Bookings by status (single round-trip via group-by).
        var statusGroups = await bookingsQ
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int byStatus(string s) => statusGroups.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        var byStatusDto = new BookingsByStatus(
            Created: byStatus(BookingStatus.Created),
            Confirmed: byStatus(BookingStatus.Confirmed),
            Completed: byStatus(BookingStatus.Completed),
            Cancelled: byStatus(BookingStatus.Cancelled),
            NoShow: byStatus(BookingStatus.NoShow));

        var totalBookings = byStatusDto.Total;

        // 2. Revenue and average ticket (visits joined to bookings via FK).
        // SQLite cannot SUM decimal server-side, so pull rows and aggregate on the client.
        var visitsQ = _db.Visits.AsNoTracking()
            .Where(v => v.Booking.StartDateTime >= fromDt && v.Booking.StartDateTime < toDt);
        if (branchId.HasValue)
            visitsQ = visitsQ.Where(v => v.Booking.BranchId == branchId.Value);

        var visitRows = await visitsQ
            .Select(v => new { v.Booking.ClientId, v.TotalAmount })
            .ToListAsync(ct);
        var revenue = visitRows.Sum(v => v.TotalAmount);
        var completedVisits = visitRows.Count;
        var averageTicket = completedVisits == 0 ? 0m : Math.Round(revenue / completedVisits, 2);

        // 3. Repeat-client rate (clients in period with >=1 completed visit, fraction with >=2).
        var clientsCompletedAgg = visitRows
            .GroupBy(v => v.ClientId)
            .Select(g => g.Count())
            .ToList();
        var clientsAtLeastOne = clientsCompletedAgg.Count;
        var repeatClients = clientsCompletedAgg.Count(c => c >= 2);
        var repeatRate = clientsAtLeastOne == 0
            ? 0m
            : Math.Round((decimal)repeatClients / clientsAtLeastOne, 4);

        // 4. Master utilization.
        // Booked minutes per master (Confirmed or Completed in scope).
        var bookedQ = _db.Bookings.AsNoTracking()
            .Where(b => b.StartDateTime >= fromDt && b.StartDateTime < toDt
                        && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed));
        if (branchId.HasValue)
            bookedQ = bookedQ.Where(b => b.BranchId == branchId.Value);

        var bookedByMaster = await bookedQ
            .GroupBy(b => b.MasterId)
            .Select(g => new { MasterId = g.Key, Minutes = g.Sum(x => x.DurationMinutes) })
            .ToListAsync(ct);

        // Work minutes per master in scope.
        var fromDate = from;
        var toDate = to;
        var workQ = _db.WorkSchedules.AsNoTracking()
            .Where(w => w.WorkDate >= fromDate && w.WorkDate <= toDate
                        && w.ScheduleType == ScheduleType.Work);
        if (branchId.HasValue)
            workQ = workQ.Where(w => w.BranchId == branchId.Value);

        // SQLite via EF can't subtract TimeOnly directly; pull rows and aggregate in memory.
        var workRows = await workQ
            .Select(w => new { w.MasterId, w.StartTime, w.EndTime })
            .ToListAsync(ct);
        var workByMaster = workRows
            .GroupBy(r => r.MasterId)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, (int)(x.EndTime - x.StartTime).TotalMinutes)));

        // Master names — pull active masters in branch scope.
        var mastersQ = _db.Masters.AsNoTracking()
            .Include(m => m.Persona)
            .Where(m => m.IsActive);
        if (branchId.HasValue)
            mastersQ = mastersQ.Where(m => m.BranchId == branchId.Value);
        var masters = await mastersQ
            .Select(m => new { m.MasterId, m.Persona.LastName, m.Persona.FirstName })
            .ToListAsync(ct);

        // Limit to masters that have any activity (booked or scheduled) in scope to keep the table compact.
        var activeMasterIds = bookedByMaster.Select(x => x.MasterId)
            .Union(workByMaster.Keys)
            .ToHashSet();

        var utilization = masters
            .Where(m => activeMasterIds.Contains(m.MasterId))
            .Select(m =>
            {
                var booked = bookedByMaster.FirstOrDefault(x => x.MasterId == m.MasterId)?.Minutes ?? 0;
                var work = workByMaster.GetValueOrDefault(m.MasterId, 0);
                var pct = work == 0 ? 0m : Math.Round((decimal)booked / work * 100m, 1);
                return new MasterUtilizationRow(
                    MasterId: m.MasterId,
                    MasterName: $"{m.LastName} {m.FirstName}".Trim(),
                    BookedMinutes: booked,
                    WorkMinutes: work,
                    UtilizationPercent: pct);
            })
            .OrderByDescending(r => r.UtilizationPercent)
            .ThenBy(r => r.MasterName)
            .ToList();

        // 5. Top services by booking count. SQLite cannot SUM decimal, so pull raw rows.
        var bookingRowsForTop = await bookingsQ
            .Select(b => new { b.ServiceId, b.Status, b.PriceSnapshot })
            .ToListAsync(ct);
        var topServices = bookingRowsForTop
            .GroupBy(b => b.ServiceId)
            .Select(g => new
            {
                ServiceId = g.Key,
                Count = g.Count(),
                Revenue = g.Where(b => b.Status == BookingStatus.Completed).Sum(b => b.PriceSnapshot),
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        var serviceNames = await _db.Services.AsNoTracking()
            .Where(s => topServices.Select(t => t.ServiceId).Contains(s.ServiceId))
            .Select(s => new { s.ServiceId, s.Name })
            .ToListAsync(ct);

        var topServicesDto = topServices
            .Select(t => new TopServiceRow(
                ServiceId: t.ServiceId,
                ServiceName: serviceNames.FirstOrDefault(n => n.ServiceId == t.ServiceId)?.Name ?? $"#{t.ServiceId}",
                Count: t.Count,
                Revenue: Math.Round(t.Revenue, 2)))
            .ToList();

        var leadsSubmittedQ = _db.Leads.AsNoTracking()
            .Where(l => l.CreatedAt >= fromDt && l.CreatedAt < toDt);
        if (branchId.HasValue)
            leadsSubmittedQ = leadsSubmittedQ.Where(l => l.PreferredBranchId == null || l.PreferredBranchId == branchId.Value);
        var leadsSubmitted = await leadsSubmittedQ.CountAsync(ct);

        var leadBookingsQ = _db.Bookings.AsNoTracking()
            .Where(b => b.Source == BookingSource.Lead && b.CreatedAt >= fromDt && b.CreatedAt < toDt);
        if (branchId.HasValue)
            leadBookingsQ = leadBookingsQ.Where(b => b.BranchId == branchId.Value);
        var leadBookingStatuses = await leadBookingsQ.Select(b => b.Status).ToListAsync(ct);
        var bookingsFromLeads = leadBookingStatuses.Count;
        var completedFromLeads = leadBookingStatuses.Count(s => s == BookingStatus.Completed);

        var salesFunnel = new SalesFunnelSnapshot(
            LeadsSubmittedInPeriod: leadsSubmitted,
            BookingsFromLeadsInPeriod: bookingsFromLeads,
            CompletedBookingsFromLeadsInPeriod: completedFromLeads);

        // Branch name (only when scoped).
        string? branchName = null;
        if (branchId.HasValue)
        {
            branchName = await _db.Branches.AsNoTracking()
                .Where(b => b.BranchId == branchId.Value)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct);
        }

        return new DashboardSnapshot(
            From: from,
            To: to,
            BranchId: branchId,
            BranchName: branchName,
            ByStatus: byStatusDto,
            TotalBookings: totalBookings,
            Revenue: Math.Round(revenue, 2),
            AverageTicket: averageTicket,
            CompletedVisits: completedVisits,
            RepeatClients: repeatClients,
            ClientsWithAtLeastOneCompletedVisit: clientsAtLeastOne,
            RepeatClientsRate: repeatRate,
            Utilization: utilization,
            TopServices: topServicesDto,
            SalesFunnel: salesFunnel);
    }

    public async Task<IReadOnlyList<BranchCompareRow>> GetBranchComparisonAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var branches = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new { b.BranchId, b.Name })
            .ToListAsync(ct);

        var rows = new List<BranchCompareRow>(branches.Count);
        foreach (var b in branches)
        {
            var snap = await GetDashboardAsync(b.BranchId, from, to, ct);
            var avgUtil = snap.Utilization.Count == 0
                ? 0m
                : Math.Round(snap.Utilization.Average(u => u.UtilizationPercent), 1);
            var total = snap.TotalBookings == 0 ? 1 : snap.TotalBookings;
            var cancelRate = Math.Round((decimal)snap.ByStatus.Cancelled / total, 4);
            var noShowRate = Math.Round((decimal)snap.ByStatus.NoShow / total, 4);
            rows.Add(new BranchCompareRow(
                BranchId: b.BranchId,
                BranchName: b.Name,
                TotalBookings: snap.TotalBookings,
                CompletedBookings: snap.ByStatus.Completed,
                Revenue: snap.Revenue,
                AverageTicket: snap.AverageTicket,
                AverageUtilizationPercent: avgUtil,
                CancelRate: cancelRate,
                NoShowRate: noShowRate));
        }
        return rows;
    }

    public async Task<IReadOnlyList<BookingExportRow>> GetExportRowsAsync(
        int? branchId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) (from, to) = (to, from);
        var fromDt = ToStart(from);
        var toDt = ToEndExclusive(to);

        var q = _db.Bookings.AsNoTracking()
            .Include(b => b.Branch)
            .Include(b => b.Master).ThenInclude(m => m.Persona)
            .Include(b => b.Service)
            .Include(b => b.Client).ThenInclude(c => c.Persona)
            .Include(b => b.Visit)
            .Where(b => b.StartDateTime >= fromDt && b.StartDateTime < toDt);
        if (branchId.HasValue)
            q = q.Where(b => b.BranchId == branchId.Value);

        var raw = await q
            .OrderBy(b => b.StartDateTime)
            .ThenBy(b => b.BookingId)
            .Select(b => new
            {
                b.BookingId,
                BranchName = b.Branch.Name,
                MasterLast = b.Master.Persona.LastName,
                MasterFirst = b.Master.Persona.FirstName,
                ServiceName = b.Service.Name,
                ClientLast = b.Client.Persona.LastName,
                ClientFirst = b.Client.Persona.FirstName,
                b.StartDateTime,
                b.DurationMinutes,
                b.PriceSnapshot,
                b.Status,
                b.Source,
                VisitTotalAmount = b.Visit == null ? (decimal?)null : (decimal?)b.Visit.TotalAmount,
                b.CancelReason,
            })
            .ToListAsync(ct);

        return raw
            .Select(r => new BookingExportRow(
                BookingId: r.BookingId,
                BranchName: r.BranchName,
                MasterName: $"{r.MasterLast} {r.MasterFirst}".Trim(),
                ServiceName: r.ServiceName,
                ClientName: $"{r.ClientLast} {r.ClientFirst}".Trim(),
                StartDateTime: r.StartDateTime,
                DurationMinutes: r.DurationMinutes,
                PriceSnapshot: r.PriceSnapshot,
                Status: r.Status,
                Source: r.Source,
                VisitTotalAmount: r.VisitTotalAmount,
                CancelReason: r.CancelReason))
            .ToList();
    }

    public async Task<ClientSegmentationSnapshot> GetClientSegmentationAsync(
        int? branchId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) (from, to) = (to, from);

        var clientRows = await GetClientAnalyticsRowsAsync(branchId, from, to, ct);
        
        if (clientRows.Count == 0)
        {
            return new ClientSegmentationSnapshot(
                From: from,
                To: to,
                BranchId: branchId,
                BranchName: await GetBranchNameAsync(branchId, ct),
                TotalClients: 0,
                AbcDistribution: new AbcDistribution(0, 0m, 0m, 0, 0m, 0m, 0, 0m, 0m),
                TierDistribution: new TierDistribution(0, 0, 0, 0),
                TopClients: Array.Empty<ClientAnalyticsRow>());
        }

        // ABC-анализ: сортируем по выручке и определяем категории
        var sortedByRevenue = clientRows.OrderByDescending(c => c.TotalRevenue).ToList();
        var totalRevenue = sortedByRevenue.Sum(c => c.TotalRevenue);
        
        var categoryA = new List<ClientAnalyticsRow>();
        var categoryB = new List<ClientAnalyticsRow>();
        var categoryC = new List<ClientAnalyticsRow>();
        
        decimal cumulativeRevenue = 0m;
        foreach (var client in sortedByRevenue)
        {
            cumulativeRevenue += client.TotalRevenue;
            var revenuePercent = totalRevenue > 0 ? cumulativeRevenue / totalRevenue : 0m;
            
            if (revenuePercent <= 0.8m)
                categoryA.Add(client);
            else if (revenuePercent <= 0.95m)
                categoryB.Add(client);
            else
                categoryC.Add(client);
        }

        var abcDistribution = new AbcDistribution(
            CategoryACount: categoryA.Count,
            CategoryARevenue: categoryA.Sum(c => c.TotalRevenue),
            CategoryARevenuePercent: totalRevenue > 0 ? Math.Round(categoryA.Sum(c => c.TotalRevenue) / totalRevenue * 100m, 2) : 0m,
            CategoryBCount: categoryB.Count,
            CategoryBRevenue: categoryB.Sum(c => c.TotalRevenue),
            CategoryBRevenuePercent: totalRevenue > 0 ? Math.Round(categoryB.Sum(c => c.TotalRevenue) / totalRevenue * 100m, 2) : 0m,
            CategoryCCount: categoryC.Count,
            CategoryCRevenue: categoryC.Sum(c => c.TotalRevenue),
            CategoryCRevenuePercent: totalRevenue > 0 ? Math.Round(categoryC.Sum(c => c.TotalRevenue) / totalRevenue * 100m, 2) : 0m);

        // Распределение по уровням
        var tierDistribution = new TierDistribution(
            NewCount: clientRows.Count(c => c.Tier == ClientTier.New),
            RegularCount: clientRows.Count(c => c.Tier == ClientTier.Regular),
            LoyalCount: clientRows.Count(c => c.Tier == ClientTier.Loyal),
            VipCount: clientRows.Count(c => c.Tier == ClientTier.VIP));

        // Топ-20 клиентов по выручке
        var topClients = sortedByRevenue.Take(20).ToList();

        return new ClientSegmentationSnapshot(
            From: from,
            To: to,
            BranchId: branchId,
            BranchName: await GetBranchNameAsync(branchId, ct),
            TotalClients: clientRows.Count,
            AbcDistribution: abcDistribution,
            TierDistribution: tierDistribution,
            TopClients: topClients);
    }

    public async Task<IReadOnlyList<ClientAnalyticsRow>> GetClientAnalyticsRowsAsync(
        int? branchId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) (from, to) = (to, from);

        var fromDt = ToStart(from);
        var toDt = ToEndExclusive(to);

        // Получаем все завершенные визиты за период
        var visitsQ = _db.Visits.AsNoTracking()
            .Include(v => v.Booking)
                .ThenInclude(b => b.Client)
                    .ThenInclude(c => c.Persona)
            .Include(v => v.Booking)
                .ThenInclude(b => b.Master)
                    .ThenInclude(m => m.Persona)
            .Include(v => v.Booking)
                .ThenInclude(b => b.Service)
            .Where(v => v.Booking.StartDateTime >= fromDt && v.Booking.StartDateTime < toDt);

        if (branchId.HasValue)
            visitsQ = visitsQ.Where(v => v.Booking.BranchId == branchId.Value);

        var visits = await visitsQ
            .Select(v => new
            {
                v.Booking.ClientId,
                ClientFirstName = v.Booking.Client.Persona.FirstName,
                ClientLastName = v.Booking.Client.Persona.LastName,
                ClientPhone = v.Booking.Client.Persona.Phone,
                ClientEmail = v.Booking.Client.Persona.Email,
                ClientSource = v.Booking.Client.Source,
                v.TotalAmount,
                v.CompletedAt,
                MasterName = v.Booking.Master.Persona.LastName + " " + v.Booking.Master.Persona.FirstName,
                ServiceName = v.Booking.Service.Name
            })
            .ToListAsync(ct);

        // Группируем по клиентам
        var clientGroups = visits
            .GroupBy(v => v.ClientId)
            .Select(g =>
            {
                var firstVisit = g.OrderBy(v => v.CompletedAt).First();
                var lastVisit = g.OrderByDescending(v => v.CompletedAt).First();
                var totalVisits = g.Count();
                var totalRevenue = g.Sum(v => v.TotalAmount);
                var avgTicket = totalRevenue / totalVisits;

                // Определяем уровень клиента
                var tier = totalVisits switch
                {
                    1 => ClientTier.New,
                    >= 2 and <= 5 => ClientTier.Regular,
                    >= 6 and <= 10 => ClientTier.Loyal,
                    _ => ClientTier.VIP
                };

                // Находим предпочитаемого мастера (с кем больше всего визитов)
                var preferredMaster = g
                    .GroupBy(v => v.MasterName)
                    .OrderByDescending(mg => mg.Count())
                    .Select(mg => mg.Key)
                    .FirstOrDefault();

                // Находим предпочитаемую услугу
                var preferredService = g
                    .GroupBy(v => v.ServiceName)
                    .OrderByDescending(sg => sg.Count())
                    .Select(sg => sg.Key)
                    .FirstOrDefault();

                var daysSinceLastVisit = (int)(DateTime.Now - lastVisit.CompletedAt).TotalDays;

                return new
                {
                    ClientId = g.Key,
                    ClientName = $"{firstVisit.ClientLastName} {firstVisit.ClientFirstName}".Trim(),
                    Phone = firstVisit.ClientPhone,
                    Email = firstVisit.ClientEmail,
                    TotalVisits = totalVisits,
                    TotalRevenue = Math.Round(totalRevenue, 2),
                    AverageTicket = Math.Round(avgTicket, 2),
                    FirstVisitDate = firstVisit.CompletedAt,
                    LastVisitDate = lastVisit.CompletedAt,
                    DaysSinceLastVisit = daysSinceLastVisit,
                    Tier = tier,
                    PreferredMaster = preferredMaster,
                    PreferredService = preferredService,
                    Source = firstVisit.ClientSource
                };
            })
            .OrderByDescending(c => c.TotalRevenue)
            .ToList();

        // Определяем ABC-категории
        var totalRevenue = clientGroups.Sum(c => c.TotalRevenue);
        var result = new List<ClientAnalyticsRow>();
        decimal cumulativeRevenue = 0m;

        foreach (var client in clientGroups)
        {
            cumulativeRevenue += client.TotalRevenue;
            var revenuePercent = totalRevenue > 0 ? cumulativeRevenue / totalRevenue : 0m;
            
            var abcCategory = revenuePercent <= 0.8m ? ClientAbcCategory.A
                : revenuePercent <= 0.95m ? ClientAbcCategory.B
                : ClientAbcCategory.C;

            result.Add(new ClientAnalyticsRow(
                ClientId: client.ClientId,
                ClientName: client.ClientName,
                Phone: client.Phone,
                Email: client.Email,
                TotalVisits: client.TotalVisits,
                TotalRevenue: client.TotalRevenue,
                AverageTicket: client.AverageTicket,
                FirstVisitDate: client.FirstVisitDate,
                LastVisitDate: client.LastVisitDate,
                DaysSinceLastVisit: client.DaysSinceLastVisit,
                AbcCategory: abcCategory,
                Tier: client.Tier,
                PreferredMaster: client.PreferredMaster,
                PreferredService: client.PreferredService,
                Source: client.Source));
        }

        return result;
    }

    private async Task<string?> GetBranchNameAsync(int? branchId, CancellationToken ct)
    {
        if (!branchId.HasValue) return null;
        
        return await _db.Branches.AsNoTracking()
            .Where(b => b.BranchId == branchId.Value)
            .Select(b => b.Name)
            .FirstOrDefaultAsync(ct);
    }

    private static DateTime ToStart(DateOnly d) => new(d.Year, d.Month, d.Day, 0, 0, 0);
    private static DateTime ToEndExclusive(DateOnly d) => new DateTime(d.Year, d.Month, d.Day, 0, 0, 0).AddDays(1);
}
