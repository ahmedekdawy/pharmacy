namespace Pharmacy.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string CreateAccessToken(
        Guid userId,
        Guid tenantId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        out DateTimeOffset expiresAt);
}
