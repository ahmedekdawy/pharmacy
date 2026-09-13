using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Catalog.Brands.Commands;

public sealed record UpdateBrandCommand(Guid Id, string NameEn, string NameAr, bool IsActive) : IRequest;
public sealed record DeleteBrandCommand(Guid Id) : IRequest;

public sealed class UpdateBrandCommandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
    }
}

public sealed class UpdateBrandCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<UpdateBrandCommand>
{
    public async Task Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Brands.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Brand not found.");
        entity.NameEn = request.NameEn.Trim();
        entity.NameAr = request.NameAr.Trim();
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteBrandCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<DeleteBrandCommand>
{
    public async Task Handle(DeleteBrandCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Brands.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Brand not found.");
        db.Brands.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
