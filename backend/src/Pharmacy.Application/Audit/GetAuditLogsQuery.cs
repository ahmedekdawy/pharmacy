using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Audit;

public sealed record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? OldValues,
    string? NewValues,
    DateTimeOffset CreatedAt);

public sealed record GetAuditLogsQuery(int Take = 100) : IRequest<IReadOnlyList<AuditLogDto>>;

public sealed class GetAuditLogsQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    public async Task<IReadOnlyList<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var take = Math.Clamp(request.Take, 1, 500);

        return await (
            from a in db.AuditLogs.AsNoTracking()
            join u in db.Users.AsNoTracking() on a.UserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            orderby a.CreatedAt descending
            select new AuditLogDto(
                a.Id,
                a.UserId,
                u != null ? u.Email : null,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.OldValues,
                a.NewValues,
                a.CreatedAt)
        ).Take(take).ToListAsync(cancellationToken);
    }
}
