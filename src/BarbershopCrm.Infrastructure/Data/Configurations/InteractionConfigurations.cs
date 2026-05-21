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


