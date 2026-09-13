using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Domain.Identity;

namespace Pharmacy.Infrastructure.Persistence.Configurations;

public sealed class RolePageConfiguration : IEntityTypeConfiguration<RolePage>
{
    public void Configure(EntityTypeBuilder<RolePage> builder)
    {
        builder.ToTable("role_pages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(x => x.PageId).HasColumnName("page_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");

        builder.HasIndex(x => new { x.TenantId, x.RoleId, x.PageId }).IsUnique().HasDatabaseName("ix_role_pages_tenant_role_page");
        builder.HasOne(x => x.Role).WithMany(x => x.RolePages).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Page).WithMany(x => x.RolePages).HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
    }
}
