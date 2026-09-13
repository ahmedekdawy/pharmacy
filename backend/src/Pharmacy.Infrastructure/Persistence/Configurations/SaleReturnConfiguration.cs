using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Infrastructure.Persistence.Configurations;

public sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        builder.ToTable("sale_returns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(64).IsRequired();
        builder.Property(x => x.SaleId).HasColumnName("sale_id").IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        builder.Property(x => x.ReturnedAt).HasColumnName("returned_at");
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 4);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(512);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.HasIndex(x => new { x.TenantId, x.Number }).IsUnique().HasDatabaseName("ix_sale_returns_tenant_id_number");
        builder.HasIndex(x => new { x.TenantId, x.SaleId }).HasDatabaseName("ix_sale_returns_tenant_id_sale_id");
        builder.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId);
        builder.HasMany(x => x.Items).WithOne(x => x.SaleReturn).HasForeignKey(x => x.SaleReturnId);
    }
}
