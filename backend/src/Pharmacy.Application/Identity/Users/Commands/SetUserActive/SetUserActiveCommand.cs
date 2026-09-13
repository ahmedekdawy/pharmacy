using MediatR;

namespace Pharmacy.Application.Identity.Users.Commands.SetUserActive;

public sealed record SetUserActiveCommand(Guid UserId, bool IsActive) : IRequest;
