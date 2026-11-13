using System.Globalization;
using FluentValidation;
using KARider.Application.Common;
using KARider.Application.Features.Auth;
using KARider.Application.Features.Calificaciones;
using KARider.Application.Features.Catalogos;
using KARider.Application.Features.Notificaciones;
using KARider.Application.Features.Perfil;
using KARider.Application.Features.Reservas;
using KARider.Application.Features.Vehiculos;
using KARider.Application.Features.Viajes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KARider.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<InstitucionOptions>()
            .Bind(configuration.GetSection(InstitucionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<SeguridadOptions>()
            .Bind(configuration.GetSection(SeguridadOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<EstadisticasOptions>()
            .Bind(configuration.GetSection(EstadisticasOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("es");
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton);

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<Notificador>();
        services.AddScoped<AuthService>();
        services.AddScoped<CatalogoService>();
        services.AddScoped<VehiculoService>();
        services.AddScoped<ViajeService>();
        services.AddScoped<RutaViajeService>();
        services.AddScoped<ReservaService>();
        services.AddScoped<CalificacionService>();
        services.AddScoped<PerfilService>();
        services.AddScoped<NotificacionService>();

        return services;
    }
}
