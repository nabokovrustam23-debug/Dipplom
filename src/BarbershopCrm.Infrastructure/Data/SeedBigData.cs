using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using BarbershopCrm.Infrastructure.Loyalty;
using BarbershopCrm.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BarbershopCrm.Infrastructure.Data;

public static class SeedBigData
{
    public const string TestPassword = "Test12345!";

    public static async Task ApplyAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (await db.Clients.CountAsync(ct) > 3)
        {
            logger.LogInformation("SeedBigData: big data already present, skipping");
            return;
        }

        logger.LogInformation("SeedBigData: seeding 25 clients with visit history...");

        var clientRoleId = await db.Roles
            .Where(r => r.Code == RoleCode.Client)
            .Select(r => r.RoleId)
            .SingleAsync(ct);

        var services = await db.Services.Where(s => s.IsActive).ToListAsync(ct);
        var masters = await db.Masters.Include(m => m.Persona).Where(m => m.IsActive).ToListAsync(ct);
        var branchIds = await db.Branches.OrderBy(b => b.BranchId).Select(b => b.BranchId).ToListAsync(ct);

        var now = DateTime.Now;
        var hash = hasher.Hash(TestPassword);

        var clients = DefineClients();
        foreach (var def in clients)
        {
            var persona = new Persona
            {
                LastName = def.LastName,
                FirstName = def.FirstName,
                MiddleName = def.MiddleName,
                Phone = def.Phone,
                Email = def.Email,
                BirthDate = def.BirthDate,
            };
            db.Persona.Add(persona);

            var user = new User
            {
                Persona = persona,
                RoleId = clientRoleId,
                BranchId = null,
                Login = def.Email,
                PasswordHash = hash.HashBase64,
                PasswordSalt = hash.SaltBase64,
                PasswordIterations = hash.Iterations,
                IsActive = true,
                CreatedAt = now,
            };
            db.Users.Add(user);

            var client = new Client
            {
                Persona = persona,
                Source = "seed-big",
                Notes = def.Notes,
                CreatedAt = now,
            };
            db.Clients.Add(client);

            await db.SaveChangesAsync(ct);

            var branchMasters = masters.Where(m => m.BranchId == def.BranchId).ToList();
            if (branchMasters.Count == 0) branchMasters = masters;

            int completedCount = 0;
            var bookingDate = now.AddMonths(-Math.Max(3, def.TargetVisits / 2));

            for (int i = 0; i < def.TargetVisits + def.FutureBookings; i++)
            {
                bool isPast = i < def.TargetVisits;

                if (!isPast)
                {
                    bookingDate = now.AddDays(1 + i - def.TargetVisits);
                }

                while (bookingDate.DayOfWeek == DayOfWeek.Sunday)
                    bookingDate = bookingDate.AddDays(1);

                var hour = 10 + (i % 9);
                var minute = (i * 17) % 4 * 15;
                var startTime = bookingDate.Date.AddHours(hour).AddMinutes(minute);

                var master = branchMasters[i % branchMasters.Count];
                var masterServices = services.Where(s =>
                    db.MasterServices.Any(ms => ms.MasterId == master.MasterId && ms.ServiceId == s.ServiceId)
                ).ToList();
                if (masterServices.Count == 0) masterServices = services;
                var service = masterServices[i % masterServices.Count];

                var discountPercent = 0m;
                var discountReason = ILoyaltyDiscountResolver.ReasonNone;

                if (isPast && completedCount == 0 && def.TargetVisits > 0)
                {
                    discountPercent = 5m;
                    discountReason = ILoyaltyDiscountResolver.ReasonFirstVisit;
                }
                else if (def.BirthDate.HasValue && IsWithinBirthdayWindow(
                    DateOnly.FromDateTime(startTime), def.BirthDate.Value, 7))
                {
                    discountPercent = 10m;
                    discountReason = ILoyaltyDiscountResolver.ReasonBirthday;
                }
                else if (completedCount >= 50)
                {
                    discountPercent = 15m;
                    discountReason = ILoyaltyDiscountResolver.ReasonTier;
                }
                else if (completedCount >= 10)
                {
                    discountPercent = 10m;
                    discountReason = ILoyaltyDiscountResolver.ReasonTier;
                }

                if (completedCount == 0 && !isPast)
                {
                    discountPercent = 0m;
                    discountReason = ILoyaltyDiscountResolver.ReasonNone;
                }

                var booking = new Booking
                {
                    ClientId = client.ClientId,
                    MasterId = master.MasterId,
                    ServiceId = service.ServiceId,
                    BranchId = master.BranchId,
                    StartDateTime = startTime,
                    DurationMinutes = service.DurationMinutes,
                    PriceSnapshot = service.Price,
                    LoyaltyDiscountPercent = discountPercent,
                    LoyaltyDiscountReason = discountReason,
                    Status = isPast ? BookingStatus.Completed : BookingStatus.Created,
                    Source = i % 3 == 0 ? BookingSource.Admin : BookingSource.Online,
                    CreatedAt = isPast ? startTime.AddHours(-2) : now,
                    UpdatedAt = isPast ? startTime.AddMinutes(service.DurationMinutes) : now,
                };
                db.Bookings.Add(booking);

                if (isPast)
                {
                    var totalAmount = service.Price * (1 - discountPercent / 100m);
                    db.Visits.Add(new Visit
                    {
                        Booking = booking,
                        TotalAmount = totalAmount,
                        MasterNotes = def.EffectiveVisitNotes.Length > 0 ? def.EffectiveVisitNotes[i % def.EffectiveVisitNotes.Length] : null,
                        CompletedAt = startTime.AddMinutes(service.DurationMinutes),
                    });
                    completedCount++;
                }

                bookingDate = bookingDate.AddDays(isPast ? 3 + (i % 5) : 7);
            }

            await db.SaveChangesAsync(ct);

            if (def.Leads > 0)
            {
                for (int l = 0; l < def.Leads; l++)
                {
                    db.Leads.Add(new Lead
                    {
                        RawName = $"{def.LastName} {def.FirstName}",
                        RawPhone = def.Phone,
                        PreferredBranchId = def.BranchId,
                        Comment = l == 0 ? "Хочу записаться на стрижку" : "Перезвоните, пожалуйста",
                        Status = l == 0 ? LeadStatus.New : LeadStatus.Done,
                        CreatedAt = now.AddDays(-30 + l * 5),
                    });
                }
                await db.SaveChangesAsync(ct);
            }

            logger.LogInformation("  ✓ {Name} — {Visits} visits, {Future} future bookings",
                $"{def.LastName} {def.FirstName}", def.TargetVisits, def.FutureBookings);
        }

        logger.LogInformation("SeedBigData: done — {Clients} clients, {Bookings} bookings, {Visits} visits",
            await db.Clients.CountAsync(ct), await db.Bookings.CountAsync(ct), await db.Visits.CountAsync(ct));
    }

    private static bool IsWithinBirthdayWindow(DateOnly bookingDate, DateOnly birthDate, int windowDays)
    {
        var candidates = new[]
        {
            GetBirthdayForYear(bookingDate.Year - 1, birthDate),
            GetBirthdayForYear(bookingDate.Year, birthDate),
            GetBirthdayForYear(bookingDate.Year + 1, birthDate)
        };

        foreach (var bday in candidates)
        {
            if (Math.Abs(bookingDate.DayNumber - bday.DayNumber) <= windowDays)
                return true;
        }

        return false;
    }

    private static DateOnly GetBirthdayForYear(int year, DateOnly birthDate)
    {
        if (birthDate.Month == 2 && birthDate.Day == 29 && !DateTime.IsLeapYear(year))
            return new DateOnly(year, 2, 28);
        return new DateOnly(year, birthDate.Month, birthDate.Day);
    }

    private static List<ClientDef> DefineClients()
    {
        var branch1 = 1;
        var branch2 = 2;

        var commonNotes = new[] {
            "Предпочитает утренние записи",
            "Любит классику, всегда мужская стрижка",
            "Постоянный клиент, ходит раз в 3 недели",
            "Часто приводит друзей",
            "Записывается через администратора",
            null, null, null, null, null
        };

        return new()
        {
            // === PREMIUM (50+ visits) ===
            new("Фёдоров", "Александр", "Дмитриевич", "+79181110001",
                BirthDate: new(1985, 3, 15), BranchId: branch1, TargetVisits: 62, FutureBookings: 3,
                Notes: "VIP-клиент, ходит с открытия салона",
                VisitNotes: commonNotes),

            new("Козлов", "Дмитрий", "Алексеевич", "+79181110002",
                BirthDate: new(1990, 7, 22), BranchId: branch1, TargetVisits: 55, FutureBookings: 2,
                Notes: "Предпочитает Артёма, всегда опасное бритьё + стрижка",
                VisitNotes: commonNotes),

            new("Новиков", "Сергей", "Игоревич", "+79181110003",
                BirthDate: new(1978, 11, 8), BranchId: branch2, TargetVisits: 58, FutureBookings: 3,
                Notes: "Камуфляж седины каждые 3 недели",
                VisitNotes: commonNotes),

            new("Попов", "Андрей", "Викторович", "+79181110004",
                BirthDate: new(1982, 1, 30), BranchId: branch2, TargetVisits: 52, FutureBookings: 2,
                Notes: "Семьянин, приводит сына",
                VisitNotes: commonNotes),

            new("Васильев", "Максим", "Олегович", "+79181110005",
                BirthDate: new(1988, 5, 12), BranchId: branch1, TargetVisits: 60, FutureBookings: 4,
                Notes: "Еженедельно коррекция бороды",
                VisitNotes: commonNotes),

            // === VIP (10-49 visits) ===
            new("Зайцев", "Иван", "Сергеевич", "+79181110006",
                BirthDate: new(1995, 9, 5), BranchId: branch1, TargetVisits: 35, FutureBookings: 2,
                VisitNotes: commonNotes),

            new("Белов", "Павел", null, "+79181110007",
                BirthDate: new(2000, 4, 18), BranchId: branch2, TargetVisits: 28, FutureBookings: 1,
                Notes: "Студент, ходит раз в месяц",
                VisitNotes: commonNotes),

            new("Карпов", "Алексей", "Николаевич", "+79181110008",
                BirthDate: new(1992, 12, 1), BranchId: branch1, TargetVisits: 22, FutureBookings: 2,
                Notes: "Коррекция бороды + стрижка",
                VisitNotes: commonNotes),

            new("Соловьёв", "Роман", "Владимирович", "+79181110009",
                BirthDate: new(1987, 6, 20), BranchId: branch2, TargetVisits: 18, FutureBookings: 1,
                VisitNotes: commonNotes),

            new("Григорьев", "Станислав", "Андреевич", "+79181110010",
                BirthDate: new(1993, 8, 14), BranchId: branch1, TargetVisits: 15, FutureBookings: 2,
                Notes: "Бреется опасной бритвой раз в 2 недели",
                VisitNotes: commonNotes),

            new("Тимофеев", "Владислав", null, "+79181110011",
                BirthDate: new(1998, 2, 28), BranchId: branch2, TargetVisits: 12, FutureBookings: 1,
                VisitNotes: commonNotes),

            new("Мельников", "Даниил", "Сергеевич", "+79181110012",
                BirthDate: new(2001, 10, 10), BranchId: branch1, TargetVisits: 14, FutureBookings: 2,
                Notes: "Ходит к Денису",
                VisitNotes: commonNotes),

            new("Тарасов", "Евгений", "Павлович", "+79181110013",
                BirthDate: new(1991, 12, 25), BranchId: branch2, TargetVisits: 20, FutureBookings: 1,
                VisitNotes: commonNotes),

            // === STANDARD (<10 visits) ===
            new("Орлов", "Артём", "Витальевич", "+79181110014",
                BirthDate: new(2002, 7, 7), BranchId: branch1, TargetVisits: 8, FutureBookings: 1,
                Notes: "Новый клиент, пришёл по рекомендации",
                VisitNotes: commonNotes),

            new("Борисов", "Константин", "Михайлович", "+79181110015",
                BirthDate: new(1996, 3, 3), BranchId: branch2, TargetVisits: 6, FutureBookings: 1,
                VisitNotes: commonNotes),

            new("Андреев", "Тимур", null, "+79181110016",
                BirthDate: new(2003, 11, 11), BranchId: branch1, TargetVisits: 5, FutureBookings: 1,
                VisitNotes: commonNotes),

            new("Николаев", "Глеб", "Романович", "+79181110017",
                BirthDate: new(1999, 6, 6), BranchId: branch2, TargetVisits: 4, FutureBookings: 0,
                Notes: "Был пару раз, пока не определился",
                VisitNotes: commonNotes),

            new("Семёнов", "Вячеслав", "Анатольевич", "+79181110018",
                BirthDate: new(1984, 9, 9), BranchId: branch1, TargetVisits: 3, FutureBookings: 1,
                VisitNotes: commonNotes),

            new("Филиппов", "Егор", "Денисович", "+79181110019",
                BirthDate: new(2004, 5, 5), BranchId: branch2, TargetVisits: 2, FutureBookings: 0,
                Notes: "Молодой клиент, стрижка машинкой",
                VisitNotes: commonNotes),

            new("Макаров", "Виктор", "Ильич", "+79181110020",
                BirthDate: new(1980, 8, 8), BranchId: branch1, TargetVisits: 1, FutureBookings: 1,
                VisitNotes: commonNotes),

            new("Дмитриев", "Валентин", null, "+79181110021",
                BranchId: branch2, TargetVisits: 0, FutureBookings: 1,
                Notes: "Новый, записался впервые",
                VisitNotes: commonNotes),

            new("Кириллов", "Матвей", "Алексеевич", "+79181110022",
                BirthDate: new(1997, 4, 4), BranchId: branch1, TargetVisits: 7, FutureBookings: 2,
                VisitNotes: commonNotes),

            new("Овчинников", "Руслан", "Сергеевич", "+79181110023",
                BirthDate: new(1994, 2, 14), BranchId: branch2, TargetVisits: 3, FutureBookings: 1,
                VisitNotes: commonNotes),

            new("Жуков", "Николай", "Петрович", "+79181110024",
                BirthDate: new(1975, 10, 20), BranchId: branch1, TargetVisits: 9, FutureBookings: 1,
                Notes: "Камуфляж седины, пенсионер",
                VisitNotes: commonNotes),

            new("Савельев", "Леонид", "Григорьевич", "+79181110025",
                BirthDate: new(1981, 12, 12), BranchId: branch2, TargetVisits: 4, FutureBookings: 1,
                VisitNotes: commonNotes),
        };
    }

    private sealed record ClientDef(
        string LastName,
        string FirstName,
        string? MiddleName,
        string Phone,
        DateOnly? BirthDate = null,
        int BranchId = 1,
        int TargetVisits = 0,
        int FutureBookings = 1,
        string? Notes = null,
        string?[]? VisitNotes = null,
        int Leads = 0)
    {
        public string Email => $"client{Phone[^4..]}@thq.ru";
        public string?[] EffectiveVisitNotes => VisitNotes ?? [];
    }
}
