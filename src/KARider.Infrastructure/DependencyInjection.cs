using System.ComponentModel.DataAnnotations;
using KARider.Application.Abstractions;
using KARider.Infrastructure.Maps;
using KARider.Infrastructure.Notifications;
using KARider.Infrastructure.Persistence;
using KARider.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KARider.Infrastructure;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Aplica migraciones pendientes al iniciar. Úsalo solo con una instancia (ver docs/despliegue-azure.md).</summary>
    public bool AplicarMigracionesAlIniciar { get; set; }

    [Range(5, 300)]
    public int CommandTimeoutSegundos { get; set; } = 30;

    [Range(0, 10)]
    public int MaxReintentos { get; set; } = 3;

    /// <summary>Instancias de DbContext reutilizables (escalamiento vertical: subir junto con los núcleos).</summary>
    [Range(16, 2048)]
    public int TamanoPoolContextos { get; set; } = 256;
}

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró la cadena de conexión a PostgreSQL. Define la variable de entorno " +
                "ConnectionStrings__DefaultConnection (ver .env.example) o usa `dotnet user-secrets`.");
        }

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var database = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContextPool<AppDbContext>(
            options => options
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.EnableRetryOnFailure(database.MaxReintentos);
                    npgsql.CommandTimeout(database.CommandTimeoutSegundos);
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history");
                })
                .UseSnakeCaseNamingConvention(),
            database.TamanoPoolContextos);
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<HashingOptions>()
            .Bind(configuration.GetSection(HashingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<ISecretHasher, HmacSecretHasher>();

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        var smtp = configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions();
        if (smtp.Configurado)
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LogEmailSender>();
        }

        services.Configure<WebPushOptions>(configuration.GetSection(WebPushOptions.SectionName));
        services.AddSingleton<IPushSender, WebPushSender>();

        // HU-08: mapas reales (OpenStreetMap) para la ruta por calles y la llegada del conductor.
        services.AddMemoryCache();
        services.AddOptions<MapasOptions>()
            .Bind(configuration.GetSection(MapasOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<IServicioMapas, ServicioMapasOsm>((sp, http) =>
        {
            var mapas = sp.GetRequiredService<IOptions<MapasOptions>>().Value;
            http.Timeout = TimeSpan.FromSeconds(mapas.TimeoutSegundos);
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", mapas.UserAgent);
        });

        return services;
    }

    /// <summary>Aplica migraciones pendientes si Database__AplicarMigracionesAlIniciar=true.</summary>
    public static async Task AplicarMigracionesSiEstaHabilitadoAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!options.AplicarMigracionesAlIniciar)
        {
            return;
        }

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("KARider.Migraciones");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> pendientes;
        try
        {
            pendientes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        }
        catch (Npgsql.NpgsqlException ex)
        {
            logger.LogCritical(
                "No se pudo conectar a PostgreSQL ({Mensaje}). Revisa ConnectionStrings__DefaultConnection " +
                "(host, puerto, usuario y contraseña) y que la base de datos esté en ejecución.",
                ex.Message);
            throw;
        }

        if (pendientes.Count > 0)
        {
            logger.LogInformation("Aplicando {Total} migración(es): {Migraciones}", pendientes.Count, string.Join(", ", pendientes));
            await db.Database.MigrateAsync(ct);
        }
    }
}
