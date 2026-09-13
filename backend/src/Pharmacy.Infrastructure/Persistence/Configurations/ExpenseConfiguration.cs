using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.Expenses;

namespace Pharmacy.Infrastructure.Persistence.Configurations;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(128).IsRequired();
        builder.Property(x => x.DescriptionEn).HasColumnName("description_en").HasMaxLength(256).IsRequired();
        builder.Property(x => x.DescriptionAr).HasColumnName("description_ar").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 4);
        builder.Property(x => x.SpentAt).HasColumnName("spent_at");
        builder.Property(x => x.CashShiftId).HasColumnName("cash_shift_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.HasIndex(x => new { x.TenantId, x.SpentAt }).HasDatabaseName("ix_expenses_tenant_id_spent_at");
    }
}
