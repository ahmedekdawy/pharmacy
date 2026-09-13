using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Catalog.Categories.Commands;

public sealed record UpdateCategoryCommand(Guid Id, string NameEn, string NameAr, bool IsActive) : IRequest;
public sealed record DeleteCategoryCommand(Guid Id) : IRequest;

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
    }
}

public sealed class UpdateCategoryCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<UpdateCategoryCommand>
{
    public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Category not found.");
        entity.NameEn = request.NameEn.Trim();
        entity.NameAr = request.NameAr.Trim();
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteCategoryCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<DeleteCategoryCommand>
{
    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Category not found.");
        db.Categories.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
