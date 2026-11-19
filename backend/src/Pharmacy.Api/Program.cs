using Asp.Versioning;
using Pharmacy.Api.Middleware;
using Pharmacy.Application;
using Pharmacy.Infrastructure;
using Pharmacy.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"));
          
    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy =>
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    });

    var app = builder.Build();

    await IdentityDataSeeder.SeedAsync(app.Services);

    // Host under /api (IIS app or same-site FTP folder) so public URLs stay /api/v1/...
    var pathBase = builder.Configuration["PathBase"];
    if (!string.IsNullOrWhiteSpace(pathBase))
    {
        app.UsePathBase(pathBase);
    }

    // Culture from Accept-Language (frontend sends the UI language). Must run before the
    // exception handler so error messages are written in the request's language.
    // Only the UI culture (message language) varies; formatting culture stays "en" so numbers
    // and dates are parsed/formatted the same way regardless of language.
    app.UseRequestLocalization(options =>
    {
        options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culture: "en", uiCulture: "ar");
        options.AddSupportedCultures("en");
        options.AddSupportedUICultures("ar", "en");
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

   // if (app.Environment.IsDevelopment())
   // {
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            // Relative URL so it also works under PathBase
            options.SwaggerEndpoint("../openapi/v1.json", "Pharmacy API v1");
            options.RoutePrefix = "swagger";
        });
   // }

    app.UseHttpsRedirection();
    app.UseCors("Frontend");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<TenantResolutionMiddleware>();

    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
