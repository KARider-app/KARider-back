using KARider.Application.Abstractions;
using KARider.Domain.Common;
using KARider.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KARider.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Carrera> Carreras => Set<Carrera>();
    public DbSet<PuntoEncuentro> PuntosEncuentro => Set<PuntoEncuentro>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Viaje> Viajes => Set<Viaje>();
    public DbSet<ParadaViaje> ParadasViaje => Set<ParadaViaje>();
    public DbSet<HistorialEdicionViaje> HistorialEdicionesViaje => Set<HistorialEdicionViaje>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<Calificacion> Calificaciones => Set<Calificacion>();
    public DbSet<CodigoVerificacion> CodigosVerificacion => Set<CodigoVerificacion>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<SuscripcionPush> SuscripcionesPush => Set<SuscripcionPush>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums como texto: legibles en la base de datos y estables ante reordenamientos.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreadoEn = ahora;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ActualizadoEn = ahora;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
