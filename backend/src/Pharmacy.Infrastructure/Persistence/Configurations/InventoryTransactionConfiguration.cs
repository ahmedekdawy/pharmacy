using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Configurations;

public sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("inventory_transactions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.TransactionType).HasColumnName("transaction_type").HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        builder.Property(x => x.ReferenceType).HasColumnName("reference_type").HasMaxLength(64);
        builder.Property(x => x.ReferenceId).HasColumnName("reference_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_inventory_transactions_tenant_id");
        builder.HasIndex(x => new { x.TenantId, x.LocationId, x.ProductId })
            .HasDatabaseName("ix_inventory_transactions_tenant_id_location_id_product_id");
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("ix_inventory_transactions_tenant_id_created_at");
    }
}
