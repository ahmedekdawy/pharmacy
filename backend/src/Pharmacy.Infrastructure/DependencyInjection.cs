using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Infrastructure.Branding;
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
        services.AddScoped<IExternalDrugLookup, HttpExternalDrugLookup>();
        services.AddHttpClient(nameof(HttpExternalDrugLookup), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PharmacyApp/1.0");
        });
        services.AddHttpClient(nameof(EgyptianDrugCatalog), client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PharmacyApp/1.0");
        });

        services.AddScoped<ITeraboxStorage, TeraboxStorage>();
        services.AddHttpClient(nameof(TeraboxStorage), (sp, client) =>
        {
            var terabox = sp.GetRequiredService<IConfiguration>().GetSection("Terabox");
            var ndus = terabox["Ndus"];
            var browserId = terabox["BrowserId"];
            var baseUrl = (terabox["BaseUrl"] ?? "").TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(ndus))
            {
                var cookie = string.IsNullOrWhiteSpace(browserId)
                    ? $"lang=en; ndus={ndus}"
                    : $"lang=en; ndus={ndus}; browserid={browserId}";
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
            }
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            // Terabox's web endpoints reject requests that don't look like they came from
            // the browser session the ndus/jsToken cookie was captured from.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", $"{baseUrl}/");
            }
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection");
          
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
