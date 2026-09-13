using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Barcode).HasColumnName("barcode").HasMaxLength(64);
        builder.Property(x => x.NameAr).HasColumnName("name_ar").HasMaxLength(256).IsRequired();
        builder.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(256).IsRequired();
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.BrandId).HasColumnName("brand_id");
        builder.Property(x => x.SellingPrice).HasColumnName("selling_price").HasPrecision(18, 4);
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_products_tenant_id");
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName("ix_products_tenant_id_code");
        builder.HasIndex(x => new { x.TenantId, x.Barcode })
            .IsUnique()
            .HasDatabaseName("ix_products_tenant_id_barcode")
            .HasFilter("barcode IS NOT NULL");
    }
}
