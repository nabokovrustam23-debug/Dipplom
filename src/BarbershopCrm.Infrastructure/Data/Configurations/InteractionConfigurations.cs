using BarbershopCrm.Domain.Entities;
using BarbershopCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BarbershopCrm.Infrastructure.Data.Configurations;

public sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> b)
    {
        b.ToTable("Leads", t =>
            t.HasCheckConstraint(
                "CK_Leads_Status",
                $"Status IN ('{LeadStatus.New}','{LeadStatus.InProgress}','{LeadStatus.Done}','{LeadStatus.Rejected}')"));

        b.HasKey(x => x.LeadId);
        b.Property(x => x.RawName).IsRequired();
        b.Property(x => x.RawPhone).IsRequired();
        b.Property(x => x.Status).IsRequired().HasDefaultValue(LeadStatus.New);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("(datetime('now'))");

        b.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_Leads_Status_Created");

        b.HasOne(x => x.Persona)
            .WithMany()
            .HasForeignKey(x => x.PersonaId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.PreferredBranch)
            .WithMany()
            .HasForeignKey(x => x.PreferredBranchId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.CreatedBooking)
            .WithMany()
            .HasForeignKey(x => x.CreatedBookingId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.ProcessedBy)
            .WithMany()
            .HasForeignKey(x => x.ProcessedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ConsentLogConfiguration : IEntityTypeConfiguration<ConsentLogEntry>
{
    public void Configure(EntityTypeBuilder<ConsentLogEntry> b)
    {
        b.ToTable("ConsentLog", t =>
            t.HasCheckConstraint(
                "CK_ConsentLog_Type",
                $"ConsentType IN ('{ConsentType.PersonalData}','{ConsentType.Marketing}')"));

        b.HasKey(x => x.ConsentId);
        b.Property(x => x.ConsentType).IsRequired();
        b.Property(x => x.AcceptedAt).HasDefaultValueSql("(datetime('now'))");

        b.HasOne(x => x.Persona)
            .WithMany()
            .HasForeignKey(x => x.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("AuditLog");
        b.HasKey(x => x.AuditId);
        b.Property(x => x.Action).IsRequired();
        b.Property(x => x.EntityType).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("(datetime('now'))");

        b.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_AuditLog_Created");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
