using System.Linq.Expressions;
using KARider.Application.Common;
using KARider.Application.Features.Catalogos;
using KARider.Domain.Entities;
using KARider.Domain.Enums;

namespace KARider.Application.Features.Viajes;

public enum OrdenViajes
{
    Horario,
    MenorAporte,
    Calificacion
}

/// <summary>Datos de ruta y gastos comunes a publicar y editar un viaje (ver <see cref="ViajeValidators"/>).</summary>
public interface ICamposRutaViaje
{
    string Destino { get; }
    double? DestinoLatitud { get; }
    double? DestinoLongitud { get; }
    int Asientos { get; }
    decimal GastoTotal { get; }
    string? Notas { get; }
    IReadOnlyList<string>? Paradas { get; }
}

public sealed record PublicarViajeRequest(
    Guid? VehiculoId,
    int PuntoEncuentroId,
    string Destino,
    double? DestinoLatitud,
    double? DestinoLongitud,
    DateTimeOffset FechaSalida,
    int Asientos,
    decimal GastoTotal,
    string? Notas,
    IReadOnlyList<string>? Paradas,
    bool Publicar = true) : ICamposRutaViaje;

public sealed record EditarViajeRequest(
    int PuntoEncuentroId,
    string Destino,
    double? DestinoLatitud,
    double? DestinoLongitud,
    DateTimeOffset FechaSalida,
    int Asientos,
    decimal GastoTotal,
    string? Notas,
    IReadOnlyList<string>? Paradas,
    string? MensajeAviso) : ICamposRutaViaje;

public sealed record CambiarEstadoViajeRequest(EstadoViaje Estado, string? Mensaje);

public sealed record UbicacionRequest(double Latitud, double Longitud);

public sealed record BuscarViajesQuery(
    string? Destino = null,
    int? PuntoEncuentroId = null,
    DateOnly? Fecha = null,
    OrdenViajes Orden = OrdenViajes.Horario,
    int Pagina = 1,
    int TamanoPagina = 20) : IPaginado;

public sealed record MisViajesQuery(EstadoViaje? Estado = null, int Pagina = 1, int TamanoPagina = 20) : IPaginado;

public sealed record ConductorResumenDto(
    Guid Id,
    string NombreCompleto,
    string Carrera,
    string? FotoUrl,
    decimal Calificacion,
    int TotalCalificaciones);

public sealed record VehiculoResumenDto(string Modelo, string Color, bool Verificado);

public sealed record ViajeResumenDto(
    Guid Id,
    ConductorResumenDto Conductor,
    PuntoEncuentroDto PuntoEncuentro,
    string Destino,
    IReadOnlyList<string> Paradas,
    DateTimeOffset FechaSalida,
    decimal AportePorAsiento,
    int AsientosDisponibles,
    int AsientosOfrecidos,
    VehiculoResumenDto Vehiculo,
    EstadoViaje Estado);

public sealed record ConductorDetalleDto(
    Guid Id,
    string NombreCompleto,
    string Carrera,
    string? FotoUrl,
    decimal Calificacion,
    int TotalCalificaciones,
    bool Verificado,
    string? Telefono);

public sealed record VehiculoDetalleDto(string Modelo, string Color, int Anio, string Placas, bool Verificado);

public sealed record ParadaDto(int Orden, string Nombre);

public sealed record PasajeroResumenDto(Guid Id, string NombreCompleto, string Carrera, string? FotoUrl, decimal Calificacion, string? Telefono);

public sealed record SolicitudReservaDto(
    Guid ReservaId,
    string Folio,
    PasajeroResumenDto Pasajero,
    int Asientos,
    decimal MontoAporte,
    EstadoReserva Estado,
    bool AportePagado,
    DateTimeOffset CreadoEn);

public sealed record MiReservaDto(Guid Id, string Folio, EstadoReserva Estado, int Asientos, decimal MontoAporte);

public sealed record ViajeDetalleDto(
    Guid Id,
    ConductorDetalleDto Conductor,
    VehiculoDetalleDto Vehiculo,
    PuntoEncuentroDto PuntoEncuentro,
    string Destino,
    double? DestinoLatitud,
    double? DestinoLongitud,
    IReadOnlyList<ParadaDto> Paradas,
    DateTimeOffset FechaSalida,
    decimal GastoTotal,
    decimal AportePorAsiento,
    int AsientosDisponibles,
    int AsientosOfrecidos,
    string? Notas,
    EstadoViaje Estado,
    bool EsPropietario,
    MiReservaDto? MiReserva,
    IReadOnlyList<SolicitudReservaDto>? Solicitudes);

public sealed record UbicacionDto(
    double Latitud,
    double Longitud,
    DateTimeOffset ActualizadaEn,
    EstadoViaje EstadoViaje,
    PuntoEncuentroDto PuntoEncuentro);

public sealed record ResumenAporteDto(
    decimal GastoTotal,
    decimal AportePorAsiento,
    int AsientosOfrecidos,
    int AsientosConfirmados,
    int PasajerosConfirmados,
    decimal TotalAportesConfirmados,
    int AportesPagados,
    decimal AporteRealPorAsiento);

internal static class ViajeProyecciones
{
    public static readonly Expression<Func<Viaje, ViajeResumenDto>> Resumen = v => new ViajeResumenDto(
        v.Id,
        new ConductorResumenDto(
            v.ConductorId,
            v.Conductor!.NombreCompleto,
            v.Conductor.Carrera!.Nombre,
            v.Conductor.FotoUrl,
            v.Conductor.CalificacionPromedio,
            v.Conductor.TotalCalificaciones),
        new PuntoEncuentroDto(
            v.PuntoEncuentro!.Id,
            v.PuntoEncuentro.Nombre,
            v.PuntoEncuentro.Descripcion,
            v.PuntoEncuentro.Latitud,
            v.PuntoEncuentro.Longitud),
        v.Destino,
        v.Paradas.OrderBy(p => p.Orden).Select(p => p.Nombre).ToList(),
        v.FechaSalida,
        v.AportePorAsiento,
        v.AsientosDisponibles,
        v.AsientosOfrecidos,
        new VehiculoResumenDto(v.Vehiculo!.Modelo, v.Vehiculo.Color, v.Vehiculo.Verificado),
        v.Estado);
}
