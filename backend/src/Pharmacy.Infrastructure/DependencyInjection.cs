using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Identity;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Tenancy;

namespace Pharmacy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IEgyptianDrugCatalog, EgyptianDrugCatalog>();
        services.AddHttpClient(nameof(EgyptianDrugCatalog), client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PharmacyApp/1.0");
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=pharmacy;Username=postgres;Password=123";

        services.AddDbContext<PharmacyDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IPharmacyDbContext>(sp => sp.GetRequiredService<PharmacyDbContext>());

        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["Key"] ?? "PharmacyDevSigningKey_ChangeMe_AtLeast32Chars!";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtSection["Issuer"] ?? "Pharmacy",
                    ValidAudience = jwtSection["Audience"] ?? "Pharmacy",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
