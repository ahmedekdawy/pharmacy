using MediatR;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Catalog;

namespace Pharmacy.Application.Catalog.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant) : IRequestHandler<CreateCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var category = new Category
        {
            TenantId = currentTenant.TenantId.Value,
            NameEn = request.NameEn.Trim(),
            NameAr = request.NameAr.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}
