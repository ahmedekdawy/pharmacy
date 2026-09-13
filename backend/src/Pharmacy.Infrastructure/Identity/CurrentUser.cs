using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Infrastructure.Identity;

public sealed class CurrentUser : ICurrentUser
{
    public Guid? UserId { get; private set; }
    public bool IsAuthenticated => UserId.HasValue;

    public void SetUser(Guid userId) => UserId = userId;
}
