using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Application.Identity.Users.Queries;
using Pharmacy.Shared.Pagination;

namespace Pharmacy.Application.Identity.Users.Queries.SearchUsers;

public sealed class SearchUsersQueryHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant) : IRequestHandler<SearchUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 ? 20 : request.PageSize > 100 ? 100 : request.PageSize;

        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Email.ToLower().Contains(term) ||
                x.FullNameEn.ToLower().Contains(term) ||
                x.FullNameAr.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(x => x.FullNameEn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new { x.Id, x.Email, x.FullNameEn, x.FullNameAr, x.IsActive, x.LastLoginAt })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(x => x.Id).ToList();
        var roleMap = await (
            from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, r.Code, RoleId = r.Id }
        ).ToListAsync(cancellationToken);

        var items = users.Select(u => new UserDto(
            u.Id,
            u.Email,
            u.FullNameEn,
            u.FullNameAr,
            u.IsActive,
            u.LastLoginAt,
            roleMap.Where(x => x.UserId == u.Id).Select(x => x.Code).Distinct().ToList(),
            roleMap.Where(x => x.UserId == u.Id).Select(x => x.RoleId).Distinct().ToList()
        )).ToList();

        return new PagedResult<UserDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
