using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.Catalog;

namespace Pharmacy.Infrastructure.Persistence.Configurations;

public sealed class DrugBarcodeConfiguration : IEntityTypeConfiguration<DrugBarcode>
{
    public void Configure(EntityTypeBuilder<DrugBarcode> builder)
    {
        builder.ToTable("drug_barcodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Barcode).HasColumnName("barcode").HasMaxLength(64).IsRequired();
        builder.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(256).IsRequired();
        builder.Property(x => x.NameAr).HasColumnName("name_ar").HasMaxLength(256).IsRequired();
        builder.Property(x => x.ScientificName).HasColumnName("scientific_name").HasMaxLength(512);
        builder.Property(x => x.Manufacturer).HasColumnName("manufacturer").HasMaxLength(256);
        builder.Property(x => x.StripsPerBox).HasColumnName("strips_per_box");
        builder.Property(x => x.UnitsPerStrip).HasColumnName("units_per_strip");
        builder.Property(x => x.PublicPrice).HasColumnName("public_price").HasPrecision(18, 4);
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(32);
        builder.Property(x => x.Confirmations).HasColumnName("confirmations");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => x.Barcode).IsUnique().HasDatabaseName("ix_drug_barcodes_barcode");
    }
}
