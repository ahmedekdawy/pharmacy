using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Shared.Localization;
using System.Reflection;

namespace Pharmacy.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(Common.Behaviors.ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Messages follow the request culture (FluentValidation ships Arabic templates);
        // property names get Arabic display names too, falling back to "Pascal Case" splitting.
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) => FieldNames.Resolve(member?.Name);
        return services;
    }
}
