using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Settings;

namespace Pharmacy.Application.Settings;

public sealed record TenantSettingDto(string Key, string Value);
public sealed record GetTenantSettingsQuery : IRequest<IReadOnlyList<TenantSettingDto>>;
public sealed record UpsertTenantSettingCommand(string Key, string Value) : IRequest;
public sealed record DeleteTenantSettingCommand(string Key) : IRequest;

public sealed class UpsertTenantSettingCommandValidator : AbstractValidator<UpsertTenantSettingCommand>
{
    public UpsertTenantSettingCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Value).NotNull().MaximumLength(2048);
    }
}

public sealed class GetTenantSettingsQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetTenantSettingsQuery, IReadOnlyList<TenantSettingDto>>
{
    public async Task<IReadOnlyList<TenantSettingDto>> Handle(GetTenantSettingsQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        return await db.TenantSettings.AsNoTracking()
            .OrderBy(x => x.Key)
            .Select(x => new TenantSettingDto(x.Key, x.Value))
            .ToListAsync(cancellationToken);
    }
}

public sealed class UpsertTenantSettingCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<UpsertTenantSettingCommand>
{
    public async Task Handle(UpsertTenantSettingCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");

        var key = request.Key.Trim();
        var existing = await db.TenantSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (existing is null)
        {
            db.TenantSettings.Add(new TenantSetting
            {
                TenantId = currentTenant.TenantId.Value,
                Key = key,
                Value = request.Value,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Value = request.Value;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteTenantSettingCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<DeleteTenantSettingCommand>
{
    public async Task Handle(DeleteTenantSettingCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var key = request.Key.Trim();
        var existing = await db.TenantSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken)
            ?? throw new InvalidOperationException("Setting not found.");
        db.TenantSettings.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }
}
