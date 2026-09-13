using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.CashManagement;

namespace Pharmacy.Infrastructure.Persistence.Configurations;

public sealed class CashShiftConfiguration : IEntityTypeConfiguration<CashShift>
{
    public void Configure(EntityTypeBuilder<CashShift> builder)
    {
        builder.ToTable("cash_shifts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        builder.Property(x => x.OpenedByUserId).HasColumnName("opened_by_user_id").IsRequired();
        builder.Property(x => x.ClosedByUserId).HasColumnName("closed_by_user_id");
        builder.Property(x => x.OpenedAt).HasColumnName("opened_at");
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
        builder.Property(x => x.OpeningCash).HasColumnName("opening_cash").HasPrecision(18, 4);
        builder.Property(x => x.ClosingCash).HasColumnName("closing_cash").HasPrecision(18, 4);
        builder.Property(x => x.ExpectedCash).HasColumnName("expected_cash").HasPrecision(18, 4);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(512);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.HasIndex(x => new { x.TenantId, x.LocationId, x.Status }).HasDatabaseName("ix_cash_shifts_tenant_id_location_id_status");
    }
}
