using MediatR;
using Pharmacy.Domain.Identity;

namespace Pharmacy.Application.Identity.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string Password,
    string FullNameEn,
    string FullNameAr,
    IReadOnlyList<Guid>? RoleIds,
    bool IsActive = true) : IRequest<Guid>;
