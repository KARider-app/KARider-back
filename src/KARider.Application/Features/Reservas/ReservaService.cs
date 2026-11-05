using FluentValidation;
using KARider.Application.Abstractions;
using KARider.Application.Common;
using KARider.Application.Features.Catalogos;
using KARider.Application.Features.Notificaciones;
using KARider.Domain.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace KARider.Application.Features.Reservas;

public sealed record CrearReservaRequest(int Asientos);

public sealed record MisReservasQuery(EstadoReserva? Estado = null, int Pagina = 1, int TamanoPagina = 20) : IPaginado;

public sealed record ConductorContactoDto(Guid Id, string NombreCompleto, string? FotoUrl, decimal Calificacion, string? Telefono);

public sealed record ViajeReservaDto(
    Guid Id,
    string Destino,
    DateTimeOffset FechaSalida,
    EstadoViaje Estado,
    PuntoEncuentroDto PuntoEncuentro,
    ConductorContactoDto Conductor,
    string Vehiculo,
    string? Placas);

public sealed record ReservaDto(
    Guid Id,
    string Folio,
    EstadoReserva Estado,
    int Asientos,
    decimal MontoAporte,
    bool AportePagado,
    DateTimeOffset CreadoEn,
    DateTimeOffset? RespondidaEn,
    ViajeReservaDto Viaje);

public sealed class CrearReservaRequestValidator : AbstractValidator<CrearReservaRequest>
{
    public CrearReservaRequestValidator() =>
        RuleFor(x => x.Asientos)
            .InclusiveBetween(1, Reserva.MaximoAsientosPorReserva)
            .WithMessage($"Puedes apartar entre 1 y {Reserva.MaximoAsientosPorReserva} asientos por reserva.");
}

public sealed class MisReservasQueryValidator : AbstractValidator<MisReservasQuery>
{
    public MisReservasQueryValidator()
    {
        this.ReglasPaginacion();
        RuleFor(x => x.Estado).IsInEnum().When(x => x.Estado.HasValue).WithMessage("El estado de la reserva no es válido.");
    }
}

/// <summary>
/// Solicitud, gestión (aceptar/rechazar) y seguimiento de reservas de asientos (P7, P8).
/// </summary>
public sealed class ReservaService(IApplicationDbContext db, Notificador notificador, TimeProvider time)
{
    public async Task<Result<ReservaDto>> CrearAsync(Guid pasajeroId, Guid viajeId, CrearReservaRequest request, CancellationToken ct)
    {
        var pasajero = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == pasajeroId && u.Activo, ct);
        if (pasajero is null)
        {
            return DomainErrors.Usuario.NoEncontrado;
        }

        var viaje = await db.Viajes.FirstOrDefaultAsync(v => v.Id == viajeId, ct);
        if (viaje is null || viaje.Estado == EstadoViaje.Borrador)
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        var existente = await db.Reservas.AsNoTracking()
            .Where(r => r.ViajeId == viajeId && r.PasajeroId == pasajeroId &&
                        (r.Estado == EstadoReserva.Pendiente || r.Estado == EstadoReserva.Confirmada))
            .Select(r => r.Folio)
            .FirstOrDefaultAsync(ct);
        if (existente is not null)
        {
            return DomainErrors.Reserva.Duplicada(existente);
        }

        var resultado = Reserva.Crear(viaje, pasajeroId, request.Asientos, time.GetUtcNow());
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        var reserva = resultado.Value;
        db.Reservas.Add(reserva);
        notificador.Agregar(
            viaje.ConductorId,
            TipoNotificacion.ReservaSolicitada,
            "Nueva reserva confirmada",
            $"{pasajero.NombreCompleto} apartó {reserva.Asientos} asiento(s) para tu viaje a {viaje.Destino} (folio {reserva.Folio}).",
            viaje.Id,
            reserva.Id);

        // El token de concurrencia del viaje evita sobreventa de asientos con solicitudes simultáneas.
        await db.SaveChangesAsync(ct);
        await notificador.DespacharAsync(ct);

        return await ObtenerInternoAsync(reserva.Id, pasajeroId, ct);
    }

    public async Task<Result<ReservaDto>> AceptarAsync(Guid conductorId, Guid reservaId, CancellationToken ct)
    {
        var (reserva, error) = await CargarParaConductorAsync(conductorId, reservaId, ct);
        if (reserva is null)
        {
            return error!;
        }

        var resultado = reserva.Confirmar(time.GetUtcNow());
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        notificador.Agregar(
            reserva.PasajeroId,
            TipoNotificacion.ReservaConfirmada,
            "¡Reserva confirmada!",
            $"Tu asiento para el viaje a {reserva.Viaje!.Destino} está asegurado (folio {reserva.Folio}). Muestra tu credencial UTTT al abordar.",
            reserva.ViajeId,
            reserva.Id);

        await db.SaveChangesAsync(ct);
        await notificador.DespacharAsync(ct);
        return await ObtenerInternoAsync(reserva.Id, conductorId, ct);
    }

    public async Task<Result<ReservaDto>> RechazarAsync(Guid conductorId, Guid reservaId, CancellationToken ct)
    {
        var (reserva, error) = await CargarParaConductorAsync(conductorId, reservaId, ct);
        if (reserva is null)
        {
            return error!;
        }

        var resultado = reserva.Rechazar(reserva.Viaje!, time.GetUtcNow());
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        notificador.Agregar(
            reserva.PasajeroId,
            TipoNotificacion.ReservaRechazada,
            "Solicitud no aceptada",
            $"El conductor no pudo aceptar tu solicitud para el viaje a {reserva.Viaje!.Destino}. Busca otro viaje disponible.",
            reserva.ViajeId,
            reserva.Id);

        await db.SaveChangesAsync(ct);
        await notificador.DespacharAsync(ct);
        return await ObtenerInternoAsync(reserva.Id, conductorId, ct);
    }

    public async Task<Result<ReservaDto>> CancelarAsync(Guid pasajeroId, Guid reservaId, CancellationToken ct)
    {
        var reserva = await db.Reservas
            .Include(r => r.Viaje)
            .Include(r => r.Pasajero)
            .FirstOrDefaultAsync(r => r.Id == reservaId, ct);

        if (reserva is null)
        {
            return DomainErrors.Reserva.NoEncontrada;
        }

        if (reserva.PasajeroId != pasajeroId)
        {
            return DomainErrors.Reserva.AccesoDenegado;
        }

        var resultado = reserva.Cancelar(reserva.Viaje!, time.GetUtcNow());
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        notificador.Agregar(
            reserva.Viaje!.ConductorId,
            TipoNotificacion.ReservaCancelada,
            "Reserva cancelada",
            $"{reserva.Pasajero!.NombreCompleto} canceló su reserva ({reserva.Asientos} asiento(s)) del viaje a {reserva.Viaje.Destino}.",
            reserva.ViajeId,
            reserva.Id);

        await db.SaveChangesAsync(ct);
        await notificador.DespacharAsync(ct);
        return await ObtenerInternoAsync(reserva.Id, pasajeroId, ct);
    }

    public async Task<Result<ReservaDto>> MarcarAportePagadoAsync(Guid conductorId, Guid reservaId, CancellationToken ct)
    {
        var (reserva, error) = await CargarParaConductorAsync(conductorId, reservaId, ct);
        if (reserva is null)
        {
            return error!;
        }

        var resultado = reserva.MarcarAportePagado();
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        await db.SaveChangesAsync(ct);
        return await ObtenerInternoAsync(reserva.Id, conductorId, ct);
    }

    public async Task<PagedResult<ReservaDto>> ListarMiasAsync(Guid pasajeroId, MisReservasQuery query, CancellationToken ct)
    {
        var reservas = db.Reservas.AsNoTracking().Where(r => r.PasajeroId == pasajeroId);
        if (query.Estado is { } estado)
        {
            reservas = reservas.Where(r => r.Estado == estado);
        }

        var pagina = await reservas
            .OrderByDescending(r => r.CreadoEn)
            .Include(r => r.Viaje).ThenInclude(v => v!.PuntoEncuentro)
            .Include(r => r.Viaje).ThenInclude(v => v!.Conductor)
            .Include(r => r.Viaje).ThenInclude(v => v!.Vehiculo)
            .ToPagedAsync(query, ct);

        return new PagedResult<ReservaDto>(pagina.Items.Select(ToDto).ToList(), pagina.Pagina, pagina.TamanoPagina, pagina.Total);
    }

    public Task<Result<ReservaDto>> ObtenerAsync(Guid usuarioId, Guid reservaId, CancellationToken ct) =>
        ObtenerInternoAsync(reservaId, usuarioId, ct);

    private async Task<Result<ReservaDto>> ObtenerInternoAsync(Guid reservaId, Guid usuarioId, CancellationToken ct)
    {
        var reserva = await db.Reservas.AsNoTracking()
            .Include(r => r.Viaje).ThenInclude(v => v!.PuntoEncuentro)
            .Include(r => r.Viaje).ThenInclude(v => v!.Conductor)
            .Include(r => r.Viaje).ThenInclude(v => v!.Vehiculo)
            .FirstOrDefaultAsync(r => r.Id == reservaId, ct);

        if (reserva is null)
        {
            return DomainErrors.Reserva.NoEncontrada;
        }

        if (reserva.PasajeroId != usuarioId && reserva.Viaje!.ConductorId != usuarioId)
        {
            return DomainErrors.Reserva.AccesoDenegado;
        }

        return ToDto(reserva);
    }

    private async Task<(Reserva? Reserva, Error? Error)> CargarParaConductorAsync(Guid conductorId, Guid reservaId, CancellationToken ct)
    {
        var reserva = await db.Reservas
            .Include(r => r.Viaje)
            .FirstOrDefaultAsync(r => r.Id == reservaId, ct);

        if (reserva is null)
        {
            return (null, DomainErrors.Reserva.NoEncontrada);
        }

        if (reserva.Viaje!.ConductorId != conductorId)
        {
            return (null, DomainErrors.Viaje.NoEsPropietario);
        }

        return (reserva, null);
    }

    private static ReservaDto ToDto(Reserva r)
    {
        var viaje = r.Viaje!;
        var conductor = viaje.Conductor!;
        var vehiculo = viaje.Vehiculo!;
        var punto = viaje.PuntoEncuentro!;
        var confirmada = r.Estado is EstadoReserva.Confirmada or EstadoReserva.Completada;

        return new ReservaDto(
            r.Id,
            r.Folio,
            r.Estado,
            r.Asientos,
            r.MontoAporte,
            r.AportePagado,
            r.CreadoEn,
            r.RespondidaEn,
            new ViajeReservaDto(
                viaje.Id,
                viaje.Destino,
                viaje.FechaSalida,
                viaje.Estado,
                new PuntoEncuentroDto(punto.Id, punto.Nombre, punto.Descripcion, punto.Latitud, punto.Longitud),
                new ConductorContactoDto(
                    conductor.Id,
                    conductor.NombreCompleto,
                    conductor.FotoUrl,
                    conductor.CalificacionPromedio,
                    confirmada ? conductor.Telefono : null),
                $"{vehiculo.Modelo} ({vehiculo.Color})",
                confirmada ? vehiculo.Placas : null));
    }
}
