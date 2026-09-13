using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Configurations;

public sealed class ProductBatchConfiguration : IEntityTypeConfiguration<ProductBatch>
{
    public void Configure(EntityTypeBuilder<ProductBatch> builder)
    {
        builder.ToTable("product_batches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.BatchNumber).HasColumnName("batch_number").HasMaxLength(128).IsRequired();
        builder.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
        builder.Property(x => x.PurchasePrice).HasColumnName("purchase_price").HasPrecision(18, 4);
        builder.Property(x => x.AvailableQuantity).HasColumnName("available_quantity").HasPrecision(18, 4);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_product_batches_tenant_id");
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.BatchNumber })
            .IsUnique()
            .HasDatabaseName("ix_product_batches_tenant_id_product_id_batch_number");
        builder.HasIndex(x => new { x.TenantId, x.ExpiryDate }).HasDatabaseName("ix_product_batches_tenant_id_expiry_date");
    }
}
