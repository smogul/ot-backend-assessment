using Microsoft.Extensions.DependencyInjection;
using OT.Assessment.Application.Services;

namespace OT.Assessment.Application;

public static class DependencyInjection
{
    /// Like a school office keeping a list of helpers. When the player controller asks for a WagerService, the office knows how to provide one.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<WagerService>();

        return services;
    }
}
