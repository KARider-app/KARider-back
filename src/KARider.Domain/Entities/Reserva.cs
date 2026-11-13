using System.Security.Cryptography;
using KARider.Domain.Common;
using KARider.Domain.Enums;
using KARider.Domain.Errors;

namespace KARider.Domain.Entities;


/// Reserva de asientos de un pasajero en un viaje. Se confirma automáticamente al crearse (HU-07).
/// Flujo: Confirmada → Completada, o Confirmada → Cancelada.

public sealed class Reserva : Entity
{
    public const int MaximoAsientosPorReserva = 2;

    private Reserva()
    {
    }

    public Guid ViajeId { get; private set; }

    public Viaje? Viaje { get; private set; }

    public Guid PasajeroId { get; private set; }

    public Usuario? Pasajero { get; private set; }

    public int Asientos { get; private set; }

    //Aporte total acordado (asientos × aporte por asiento).
    public decimal MontoAporte { get; private set; }

    /// Folio legible para el pasajero.
    public string Folio { get; private set; } = string.Empty;

    public EstadoReserva Estado { get; private set; }

    public bool AportePagado { get; private set; }

    public DateTimeOffset? RespondidaEn { get; private set; }

    public bool EstaActiva => Estado is EstadoReserva.Pendiente or EstadoReserva.Confirmada;

    public static string GenerarFolio(DateTimeOffset ahora) =>
        $"KAR-UTTT-{ahora.Year}-{RandomNumberGenerator.GetInt32(0, 1_000_000):D6}";

    public static Result<Reserva> Crear(Viaje viaje, Guid pasajeroId, int asientos, DateTimeOffset ahora)
    {
        if (viaje.ConductorId == pasajeroId)
        {
            return DomainErrors.Reserva.ViajePropio;
        }

        if (asientos is < 1 or > MaximoAsientosPorReserva)
        {
            return DomainErrors.Reserva.AsientosInvalidos(MaximoAsientosPorReserva);
        }

        var apartado = viaje.ApartarAsientos(asientos, ahora);
        if (apartado.IsFailure)
        {
            return apartado.Error;
        }

        return new Reserva
        {
            ViajeId = viaje.Id,
            PasajeroId = pasajeroId,
            Asientos = asientos,
            MontoAporte = asientos * viaje.AportePorAsiento,
            Folio = GenerarFolio(ahora),
            // HU-07: si hay cupo, la reserva queda confirmada de inmediato (sin aprobación del conductor).
            Estado = EstadoReserva.Confirmada,
            RespondidaEn = ahora
        };
    }

    public Result Confirmar(DateTimeOffset ahora)
    {
        if (Estado != EstadoReserva.Pendiente)
        {
            return DomainErrors.Reserva.EstadoInvalido(Estado, "confirmar");
        }

        Estado = EstadoReserva.Confirmada;
        RespondidaEn = ahora;
        return Result.Success();
    }

    public Result Rechazar(Viaje viaje, DateTimeOffset ahora)
    {
        if (Estado != EstadoReserva.Pendiente)
        {
            return DomainErrors.Reserva.EstadoInvalido(Estado, "rechazar");
        }

        Estado = EstadoReserva.Rechazada;
        RespondidaEn = ahora;
        viaje.LiberarAsientos(Asientos);
        return Result.Success();
    }

    ///Cancelación hecha por el pasajero antes de que el viaje inicie en un tiempo 
    public Result Cancelar(Viaje viaje, DateTimeOffset ahora)
    {
        if (!EstaActiva || viaje.Estado != EstadoViaje.Programado)
        {
            return DomainErrors.Reserva.EstadoInvalido(Estado, "cancelar");
        }

        Estado = EstadoReserva.Cancelada;
        RespondidaEn = ahora;
        viaje.LiberarAsientos(Asientos);
        return Result.Success();
    }

    public Result MarcarAportePagado()
    {
        if (Estado is not (EstadoReserva.Confirmada or EstadoReserva.Completada))
        {
            return DomainErrors.Reserva.EstadoInvalido(Estado, "registrar el pago del aporte");
        }

        AportePagado = true;
        return Result.Success();
    }

    internal void Completar(DateTimeOffset ahora)
    {
        Estado = EstadoReserva.Completada;
        RespondidaEn ??= ahora;
    }

    internal void Expirar(DateTimeOffset ahora)
    {
        Estado = EstadoReserva.Rechazada;
        RespondidaEn = ahora;
    }

    internal void CancelarPorViaje(DateTimeOffset ahora)
    {
        Estado = EstadoReserva.Cancelada;
        RespondidaEn = ahora;
    }
}
