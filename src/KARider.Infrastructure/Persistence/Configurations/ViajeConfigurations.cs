using KARider.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KARider.Infrastructure.Persistence.Configurations;

internal sealed class ViajeConfiguration : IEntityTypeConfiguration<Viaje>
{
    public void Configure(EntityTypeBuilder<Viaje> builder)
    {
        builder.ToTable("viajes", t =>
        {
            t.HasCheckConstraint("ck_viajes_asientos", "asientos_disponibles >= 0 AND asientos_disponibles <= asientos_ofrecidos");
            t.HasCheckConstraint("ck_viajes_gasto", "gasto_total > 0");
            t.HasCheckConstraint("ck_viajes_numero_ediciones", "numero_ediciones >= 0");
        });

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Destino).HasMaxLength(150).IsRequired();
        builder.Property(v => v.Notas).HasMaxLength(500);

        // Evita sobreventa de asientos ante reservas simultáneas.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasOne(v => v.Conductor).WithMany().HasForeignKey(v => v.ConductorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.Vehiculo).WithMany().HasForeignKey(v => v.VehiculoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.PuntoEncuentro).WithMany().HasForeignKey(v => v.PuntoEncuentroId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Paradas).WithOne().HasForeignKey(p => p.ViajeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.Reservas).WithOne(r => r.Viaje).HasForeignKey(r => r.ViajeId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(v => v.Paradas).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(v => v.Reservas).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(v => v.AsientosReservados);
        builder.Ignore(v => v.EsEditable);

        // Búsqueda principal: viajes programados ordenados por salida.
        builder.HasIndex(v => new { v.Estado, v.FechaSalida });
        builder.HasIndex(v => new { v.ConductorId, v.FechaSalida });
        // HU-06: filtro por destino (el índice por punto_encuentro_id ya lo crea la llave foránea).
        builder.HasIndex(v => v.Destino);
    }
}

internal sealed class HistorialEdicionViajeConfiguration : IEntityTypeConfiguration<HistorialEdicionViaje>
{
    public void Configure(EntityTypeBuilder<HistorialEdicionViaje> builder)
    {
        builder.ToTable("historial_ediciones_viaje");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.MensajeAviso).HasMaxLength(500);

        builder.HasOne<Viaje>().WithMany().HasForeignKey(h => h.ViajeId).OnDelete(DeleteBehavior.Cascade);

        // Consulta del historial de un viaje en orden cronológico.
        builder.HasIndex(h => new { h.ViajeId, h.CreadoEn });
    }
}

internal sealed class ParadaViajeConfiguration : IEntityTypeConfiguration<ParadaViaje>
{
    public void Configure(EntityTypeBuilder<ParadaViaje> builder)
    {
        builder.ToTable("paradas_viaje");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => new { p.ViajeId, p.Orden }).IsUnique();
    }
}

internal sealed class ReservaConfiguration : IEntityTypeConfiguration<Reserva>
{
    public void Configure(EntityTypeBuilder<Reserva> builder)
    {
        builder.ToTable("reservas", t =>
            t.HasCheckConstraint("ck_reservas_asientos", $"asientos BETWEEN 1 AND {Reserva.MaximoAsientosPorReserva}"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Folio).HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Folio).IsUnique();

        builder.HasOne(r => r.Pasajero).WithMany().HasForeignKey(r => r.PasajeroId).OnDelete(DeleteBehavior.Restrict);

        // Un pasajero solo puede tener una reserva activa por viaje.
        builder.HasIndex(r => new { r.ViajeId, r.PasajeroId })
            .IsUnique()
            .HasFilter("estado IN ('Pendiente', 'Confirmada')");
        builder.HasIndex(r => new { r.PasajeroId, r.Estado });

        builder.Ignore(r => r.EstaActiva);
    }
}

internal sealed class CalificacionConfiguration : IEntityTypeConfiguration<Calificacion>
{
    public void Configure(EntityTypeBuilder<Calificacion> builder)
    {
        builder.ToTable("calificaciones", t =>
            t.HasCheckConstraint("ck_calificaciones_estrellas", "estrellas BETWEEN 1 AND 5"));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Comentario).HasMaxLength(500);
        builder.Property(c => c.Etiquetas).HasColumnType("text[]");

        builder.HasOne(c => c.Viaje).WithMany().HasForeignKey(c => c.ViajeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Evaluador).WithMany().HasForeignKey(c => c.EvaluadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Evaluado).WithMany().HasForeignKey(c => c.EvaluadoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.ViajeId, c.EvaluadorId, c.EvaluadoId }).IsUnique();
        builder.HasIndex(c => new { c.EvaluadoId, c.CreadoEn });
    }
}
