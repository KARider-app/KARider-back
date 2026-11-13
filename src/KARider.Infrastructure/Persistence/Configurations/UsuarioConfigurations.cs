using KARider.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KARider.Infrastructure.Persistence.Configurations;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios", t =>
        {
            t.HasCheckConstraint("ck_usuarios_cuatrimestre", "cuatrimestre IS NULL OR cuatrimestre BETWEEN 1 AND 11");
            t.HasCheckConstraint("ck_usuarios_calificacion", "calificacion_promedio BETWEEN 0 AND 5");
        });

        builder.HasKey(u => u.Id);
        builder.Property(u => u.NombreCompleto).HasMaxLength(120).IsRequired();
        builder.Property(u => u.Matricula).HasMaxLength(10).IsRequired();
        builder.Property(u => u.Telefono).HasMaxLength(20).IsRequired();
        builder.Property(u => u.CorreoInstitucional).HasMaxLength(150).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.FotoUrl).HasMaxLength(500);
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(u => u.CalificacionPromedio).HasPrecision(3, 2);

        // Concurrencia optimista con la columna de sistema xmin de PostgreSQL.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(u => u.CorreoInstitucional).IsUnique();
        builder.HasIndex(u => u.Matricula).IsUnique();

        builder.HasOne(u => u.Carrera).WithMany().HasForeignKey(u => u.CarreraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(u => u.Vehiculos).WithOne(v => v.Conductor).HasForeignKey(v => v.ConductorId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(u => u.Vehiculos).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(u => u.EsConductor);
    }
}

internal sealed class VehiculoConfiguration : IEntityTypeConfiguration<Vehiculo>
{
    public void Configure(EntityTypeBuilder<Vehiculo> builder)
    {
        builder.ToTable("vehiculos", t =>
            t.HasCheckConstraint("ck_vehiculos_capacidad", $"capacidad BETWEEN 1 AND {Vehiculo.CapacidadMaxima}"));

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Modelo).HasMaxLength(80).IsRequired();
        builder.Property(v => v.Color).HasMaxLength(30).IsRequired();
        builder.Property(v => v.Placas).HasMaxLength(10).IsRequired();

        // Unas placas solo pueden estar activas en un vehículo.
        builder.HasIndex(v => v.Placas).IsUnique().HasFilter("activo = true");
        builder.HasIndex(v => v.ConductorId);
    }
}

internal sealed class CarreraConfiguration : IEntityTypeConfiguration<Carrera>
{
    public void Configure(EntityTypeBuilder<Carrera> builder)
    {
        builder.ToTable("carreras");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
        builder.HasIndex(c => c.Nombre).IsUnique();

        // Catálogo inicial de carreras UTTT: validar y ajustar con servicios escolares.
        builder.HasData(
            new Carrera { Id = 1, Nombre = "Ingeniería en Desarrollo y Gestión de Software" },
            new Carrera { Id = 2, Nombre = "Ingeniería en Mecatrónica" },
            new Carrera { Id = 3, Nombre = "Ingeniería en Tecnologías de la Información e Innovación Digital" },
            new Carrera { Id = 4, Nombre = "Ingeniería en Mantenimiento Industrial" },
            new Carrera { Id = 5, Nombre = "Ingeniería en Procesos y Operaciones Industriales" },
            new Carrera { Id = 6, Nombre = "Ingeniería en Energías Renovables" },
            new Carrera { Id = 7, Nombre = "Ingeniería Química" },
            new Carrera { Id = 8, Nombre = "Ingeniería en Logística Internacional" },
            new Carrera { Id = 9, Nombre = "Ingeniería Civil" },
            new Carrera { Id = 10, Nombre = "Licenciatura en Contaduría" },
            new Carrera { Id = 11, Nombre = "Licenciatura en Negocios y Mercadotecnia" },
            new Carrera { Id = 12, Nombre = "Licenciatura en Administración" });
    }
}

internal sealed class PuntoEncuentroConfiguration : IEntityTypeConfiguration<PuntoEncuentro>
{
    public void Configure(EntityTypeBuilder<PuntoEncuentro> builder)
    {
        builder.ToTable("puntos_encuentro");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Descripcion).HasMaxLength(300).IsRequired();

        // Coordenadas reales del campus UTTT (Carretera Tula-Tepeji) tomadas de OpenStreetMap: contorno del
        // campus (way 386380501), estacionamiento (way 907217233) y accesos desde la carretera. Sprint 4 / HU-08.
        builder.HasData(
            new PuntoEncuentro
            {
                Id = 1,
                Nombre = "Puerta Principal (Torniquetes Acceso A)",
                Descripcion = "Junto a la caseta de vigilancia, frente a los torniquetes de acceso peatonal.",
                Latitud = 20.00997,
                Longitud = -99.34250
            },
            new PuntoEncuentro
            {
                Id = 2,
                Nombre = "Edificio B (Explanada de Sistemas · IDGS)",
                Descripcion = "Explanada frente al Edificio B, área de Desarrollo y Gestión de Software.",
                Latitud = 20.00995,
                Longitud = -99.34395
            },
            new PuntoEncuentro
            {
                Id = 3,
                Nombre = "Estacionamiento principal",
                Descripcion = "Entrada vehicular del estacionamiento principal del campus.",
                Latitud = 20.00982,
                Longitud = -99.34285
            });
    }
}
