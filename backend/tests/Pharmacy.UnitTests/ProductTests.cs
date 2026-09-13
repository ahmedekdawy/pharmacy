using Pharmacy.Domain.Catalog;

namespace Pharmacy.UnitTests;

public class ProductTests
{
    [Fact]
    public void Product_Is_Tenant_Owned()
    {
        var tenantId = Guid.NewGuid();
        var product = new Product
        {
            TenantId = tenantId,
            Code = "AUG-625",
            NameAr = "أوجمنتين",
            NameEn = "Augmentin",
            SellingPrice = 85.5m
        };

        Assert.Equal(tenantId, product.TenantId);
        Assert.False(product.IsDeleted);
        Assert.True(product.Id != Guid.Empty);
    }
}
