using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.Infrastructure.Persistence.Configurations;

public sealed class ShortageConfiguration : IEntityTypeConfiguration<Shortage>
{
    public void Configure(EntityTypeBuilder<Shortage> builder)
    {
        builder.ToTable("shortages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.ItemName).HasColumnName("item_name").HasMaxLength(256).IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id");
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        builder.Property(x => x.RequestCount).HasColumnName("request_count");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(512);
        builder.Property(x => x.CustomerName).HasColumnName("customer_name").HasMaxLength(128);
        builder.Property(x => x.CustomerPhone).HasColumnName("customer_phone").HasMaxLength(32);
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(32).IsRequired();
        builder.Property(x => x.RequestedAt).HasColumnName("requested_at");
        builder.Property(x => x.LastRequestedAt).HasColumnName("last_requested_at");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_shortages_tenant_id_status");
        builder.HasIndex(x => new { x.TenantId, x.ProductId }).HasDatabaseName("ix_shortages_tenant_id_product_id");
        builder.HasIndex(x => new { x.TenantId, x.LastRequestedAt }).HasDatabaseName("ix_shortages_tenant_id_last_requested_at");
    }
}
