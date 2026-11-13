using KARider.Application.Abstractions;
using KARider.Domain.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace KARider.Application.Features.Viajes;

public sealed record PuntoRutaDto(double Latitud, double Longitud, string Nombre);

/// <summary>
/// Ruta del viaje para dibujarla en el mapa. <c>Trazo</c> son pares [latitud, longitud]. Si
/// <c>PorCalles</c> es false, el servicio de rutas no respondió y el trazo es una línea recta de respaldo.
/// </summary>
public sealed record RutaViajeDto(
    PuntoRutaDto Origen,
    PuntoRutaDto? Destino,
    IReadOnlyList<PuntoRutaDto> Paradas,
    IReadOnlyList<double[]> Trazo,
    double? DistanciaKm,
    double? DuracionMinutos,
    bool PorCalles);

public enum ObjetivoLlegada
{
    /// <summary>Viaje programado: el conductor va hacia el punto de encuentro.</summary>
    PuntoEncuentro,

    /// <summary>Viaje en curso: el conductor va hacia el destino.</summary>
    Destino
}

/// <summary>Tiempo estimado de llegada y distancia restante del conductor, calculados por calles.</summary>
public sealed record LlegadaConductorDto(
    ObjetivoLlegada Objetivo,
    IReadOnlyList<double[]> Trazo,
    double DistanciaKm,
    double DuracionMinutos,
    bool PorCalles);

/// <summary>HU-08: ruta real del viaje (por calles) y llegada del conductor para el mapa P8.</summary>
public sealed class RutaViajeService(IApplicationDbContext db, IServicioMapas mapas)
{
    /// <summary>Velocidad urbana promedio para el respaldo en línea recta.</summary>
    public const double VelocidadRespaldoKmH = 30;

    /// <summary>
    /// Ruta punto de encuentro → paradas → destino. Cualquier usuario autenticado puede ver la ruta de un
    /// viaje publicado (igual que su detalle); los borradores solo los ve su conductor.
    /// </summary>
    public async Task<Result<RutaViajeDto>> ObtenerRutaAsync(Guid usuarioId, Guid viajeId, CancellationToken ct)
    {
        var viaje = await db.Viajes
            .Include(v => v.PuntoEncuentro)
            .Include(v => v.Paradas)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct);

        if (viaje is null || (viaje.Estado == EstadoViaje.Borrador && viaje.ConductorId != usuarioId))
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        var punto = viaje.PuntoEncuentro!;
        var origen = new PuntoRutaDto(punto.Latitud, punto.Longitud, punto.Nombre);
        var cerca = new PuntoGeo(punto.Latitud, punto.Longitud);

        var destino = await ObtenerDestinoAsync(viaje, ct);

        var paradas = new List<PuntoRutaDto>();
        foreach (var parada in viaje.Paradas.OrderBy(p => p.Orden))
        {
            if (await mapas.GeocodificarAsync(parada.Nombre, cerca, ct) is { } geo)
            {
                paradas.Add(new PuntoRutaDto(geo.Latitud, geo.Longitud, parada.Nombre));
            }
        }

        if (destino is null)
        {
            // Sin destino ubicable solo se puede mostrar el punto de encuentro (y las paradas encontradas).
            return new RutaViajeDto(origen, null, paradas, [], null, null, false);
        }

        var puntos = new List<PuntoGeo> { cerca };
        puntos.AddRange(paradas.Select(p => new PuntoGeo(p.Latitud, p.Longitud)));
        puntos.Add(new PuntoGeo(destino.Latitud, destino.Longitud));

        var ruta = await CalcularConRespaldoAsync(puntos, ct);
        return new RutaViajeDto(origen, destino, paradas, ruta.Trazo, ruta.DistanciaKm, ruta.DuracionMinutos, ruta.PorCalles);
    }

    /// <summary>
    /// Criterio 1: tiempo estimado de llegada y distancia restante del conductor, por calles.
    /// Regla 1: solo con el viaje programado o en curso. Regla 2: solo el conductor y pasajeros confirmados.
    /// </summary>
    public async Task<Result<LlegadaConductorDto>> ObtenerLlegadaAsync(Guid usuarioId, Guid viajeId, CancellationToken ct)
    {
        var viaje = await db.Viajes
            .Include(v => v.PuntoEncuentro)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct);

        if (viaje is null)
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        if (!await EsParticipanteConfirmadoAsync(viaje, usuarioId, ct))
        {
            return DomainErrors.Viaje.AccesoDenegado;
        }

        if (viaje.Estado is not (EstadoViaje.Programado or EstadoViaje.EnCurso)
            || viaje.UbicacionLatitud is not { } lat || viaje.UbicacionLongitud is not { } lng)
        {
            return DomainErrors.Viaje.UbicacionNoDisponible;
        }

        var conductor = new PuntoGeo(lat, lng);
        var objetivo = ObjetivoLlegada.PuntoEncuentro;
        var hacia = new PuntoGeo(viaje.PuntoEncuentro!.Latitud, viaje.PuntoEncuentro.Longitud);

        if (viaje.Estado == EstadoViaje.EnCurso && await ObtenerDestinoAsync(viaje, ct) is { } destino)
        {
            objetivo = ObjetivoLlegada.Destino;
            hacia = new PuntoGeo(destino.Latitud, destino.Longitud);
        }

        var ruta = await CalcularConRespaldoAsync([conductor, hacia], ct);
        return new LlegadaConductorDto(objetivo, ruta.Trazo, ruta.DistanciaKm ?? 0, ruta.DuracionMinutos ?? 0, ruta.PorCalles);
    }

    /// <summary>Coordenadas del destino; si el viaje no las tiene, se geocodifica el texto y se guardan.</summary>
    private async Task<PuntoRutaDto?> ObtenerDestinoAsync(Viaje viaje, CancellationToken ct)
    {
        if (viaje.DestinoLatitud is not { } lat || viaje.DestinoLongitud is not { } lng)
        {
            var cerca = new PuntoGeo(viaje.PuntoEncuentro!.Latitud, viaje.PuntoEncuentro.Longitud);
            if (await mapas.GeocodificarAsync(viaje.Destino, cerca, ct) is not { } geo)
            {
                return null;
            }

            if (viaje.CompletarCoordenadasDestino(geo.Latitud, geo.Longitud))
            {
                await db.SaveChangesAsync(ct);
            }

            (lat, lng) = (geo.Latitud, geo.Longitud);
        }

        return new PuntoRutaDto(lat, lng, viaje.Destino);
    }

    private async Task<(IReadOnlyList<double[]> Trazo, double? DistanciaKm, double? DuracionMinutos, bool PorCalles)>
        CalcularConRespaldoAsync(IReadOnlyList<PuntoGeo> puntos, CancellationToken ct)
    {
        if (await mapas.CalcularRutaAsync(puntos, ct) is { } ruta)
        {
            return (ATrazo(ruta.Trazo), Math.Round(ruta.DistanciaKm, 1), Math.Round(ruta.DuracionMinutos, 0), true);
        }

        // Respaldo: línea recta a velocidad urbana, para que el mapa nunca quede vacío.
        var km = 0.0;
        for (var i = 1; i < puntos.Count; i++)
        {
            km += DistanciaKm(puntos[i - 1], puntos[i]);
        }

        return (ATrazo(puntos), Math.Round(km, 1), Math.Round(km / VelocidadRespaldoKmH * 60, 0), false);
    }

    private static List<double[]> ATrazo(IEnumerable<PuntoGeo> puntos) =>
        puntos.Select(p => new[] { p.Latitud, p.Longitud }).ToList();

    /// <summary>Distancia en línea recta (Haversine), en km.</summary>
    public static double DistanciaKm(PuntoGeo a, PuntoGeo b)
    {
        const double radioTierraKm = 6371.0;
        static double Rad(double g) => g * Math.PI / 180.0;
        var dLat = Rad(b.Latitud - a.Latitud);
        var dLon = Rad(b.Longitud - a.Longitud);
        var h = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(Rad(a.Latitud)) * Math.Cos(Rad(b.Latitud)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * radioTierraKm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private async Task<bool> EsParticipanteConfirmadoAsync(Viaje viaje, Guid usuarioId, CancellationToken ct) =>
        viaje.ConductorId == usuarioId ||
        await db.Reservas.AnyAsync(
            r => r.ViajeId == viaje.Id && r.PasajeroId == usuarioId && r.Estado == EstadoReserva.Confirmada,
            ct);
}
