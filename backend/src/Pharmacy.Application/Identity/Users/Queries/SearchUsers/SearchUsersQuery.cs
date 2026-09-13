using MediatR;
using Pharmacy.Shared.Pagination;

namespace Pharmacy.Application.Identity.Users.Queries.SearchUsers;

public sealed record SearchUsersQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<UserDto>>;
