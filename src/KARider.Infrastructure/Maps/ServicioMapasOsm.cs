using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KARider.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KARider.Infrastructure.Maps;

public sealed class MapasOptions
{
    public const string SectionName = "Mapas";

    /// <summary>Servicio de geocodificación compatible con Nominatim (OpenStreetMap).</summary>
    [Required, Url]
    public string UrlGeocodificacion { get; set; } = "https://nominatim.openstreetmap.org";

    /// <summary>Servicio de rutas compatible con OSRM.</summary>
    [Required, Url]
    public string UrlRutas { get; set; } = "https://router.project-osrm.org";

    /// <summary>Identificación de la app que exige la política de uso de Nominatim.</summary>
    [Required]
    public string UserAgent { get; set; } = "KARider/1.0 (PWA de carpooling UTTT; +https://github.com/aric92045-blip/KARider-back)";

    [Range(1, 60)]
    public int TimeoutSegundos { get; set; } = 8;

    /// <summary>Radio (en grados, ~111 km por grado) en el que se priorizan los resultados de geocodificación.</summary>
    [Range(0.1, 10)]
    public double RadioBusquedaGrados { get; set; } = 1.0;

    /// <summary>Pausa mínima entre consultas a Nominatim (su política permite 1 por segundo).</summary>
    [Range(0, 10)]
    public double SegundosEntreConsultas { get; set; } = 1.1;
}

/// <summary>
/// HU-08: geocodificación (Nominatim) y rutas por calles (OSRM) sobre OpenStreetMap. Los resultados se guardan en
/// memoria para no repetir consultas: Nominatim permite como máximo 1 petición por segundo.
/// </summary>
public sealed class ServicioMapasOsm(
    HttpClient http,
    IMemoryCache cache,
    IOptions<MapasOptions> opciones,
    ILogger<ServicioMapasOsm> logger) : IServicioMapas
{
    private static readonly SemaphoreSlim TurnoNominatim = new(1, 1);
    private static DateTimeOffset _ultimaConsultaNominatim = DateTimeOffset.MinValue;

    private static readonly TimeSpan VigenciaGeocodificacion = TimeSpan.FromDays(7);
    private static readonly TimeSpan VigenciaRuta = TimeSpan.FromHours(6);

    public async Task<PuntoGeo?> GeocodificarAsync(string direccion, PuntoGeo cercaDe, CancellationToken cancellationToken = default)
    {
        var texto = direccion.Trim();
        if (texto.Length == 0)
        {
            return null;
        }

        var clave = $"geo:{texto.ToLowerInvariant()}:{cercaDe.Latitud:F2},{cercaDe.Longitud:F2}";
        if (cache.TryGetValue(clave, out PuntoGeo? guardado))
        {
            return guardado;
        }

        // Primero dentro de la zona del campus; si no hay resultado, en todo México.
        var punto = await ConsultarNominatimAsync(texto, cercaDe, acotado: true, cancellationToken)
            ?? await ConsultarNominatimAsync(texto, cercaDe, acotado: false, cancellationToken);

        if (punto is not null)
        {
            cache.Set(clave, punto, VigenciaGeocodificacion);
        }

        return punto;
    }

    public async Task<RutaCalculada?> CalcularRutaAsync(IReadOnlyList<PuntoGeo> puntos, CancellationToken cancellationToken = default)
    {
        if (puntos.Count < 2)
        {
            return null;
        }

        var coordenadas = string.Join(';', puntos.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Longitud:F5},{p.Latitud:F5}")));
        var clave = "ruta:" + coordenadas;
        if (cache.TryGetValue(clave, out RutaCalculada? guardada))
        {
            return guardada;
        }

        var url = $"{opciones.Value.UrlRutas.TrimEnd('/')}/route/v1/driving/{coordenadas}?overview=full&geometries=geojson";
        try
        {
            var respuesta = await http.GetFromJsonAsync<RespuestaOsrm>(url, cancellationToken);
            var ruta = respuesta?.Routes?.FirstOrDefault();
            if (respuesta?.Code != "Ok" || ruta?.Geometry?.Coordinates is not { Count: > 1 } linea)
            {
                logger.LogWarning("El servicio de rutas no devolvió una ruta ({Codigo}).", respuesta?.Code);
                return null;
            }

            var calculada = new RutaCalculada(
                linea.Where(c => c.Length >= 2).Select(c => new PuntoGeo(c[1], c[0])).ToList(),
                ruta.Distance / 1000.0,
                ruta.Duration / 60.0);
            cache.Set(clave, calculada, VigenciaRuta);
            return calculada;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "No se pudo calcular la ruta por calles; se usará línea recta.");
            return null;
        }
    }

    private async Task<PuntoGeo?> ConsultarNominatimAsync(string texto, PuntoGeo cerca, bool acotado, CancellationToken ct)
    {
        var radio = opciones.Value.RadioBusquedaGrados;
        var caja = string.Create(
            CultureInfo.InvariantCulture,
            $"{cerca.Longitud - radio:F4},{cerca.Latitud + radio:F4},{cerca.Longitud + radio:F4},{cerca.Latitud - radio:F4}");
        var url = $"{opciones.Value.UrlGeocodificacion.TrimEnd('/')}/search?format=jsonv2&limit=1&countrycodes=mx" +
                  $"&accept-language=es&viewbox={caja}&bounded={(acotado ? 1 : 0)}&q={Uri.EscapeDataString(texto)}";

        await TurnoNominatim.WaitAsync(ct);
        try
        {
            var espera = _ultimaConsultaNominatim.AddSeconds(opciones.Value.SegundosEntreConsultas) - DateTimeOffset.UtcNow;
            if (espera > TimeSpan.Zero)
            {
                await Task.Delay(espera, ct);
            }

            _ultimaConsultaNominatim = DateTimeOffset.UtcNow;
            var resultados = await http.GetFromJsonAsync<List<ResultadoNominatim>>(url, ct);
            var primero = resultados?.FirstOrDefault();
            return primero is not null
                   && double.TryParse(primero.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                   && double.TryParse(primero.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
                ? new PuntoGeo(lat, lon)
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "No se pudo geocodificar la dirección del viaje.");
            return null;
        }
        finally
        {
            TurnoNominatim.Release();
        }
    }

    private sealed record ResultadoNominatim(
        [property: JsonPropertyName("lat")] string? Lat,
        [property: JsonPropertyName("lon")] string? Lon);

    private sealed record RespuestaOsrm(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("routes")] List<RutaOsrm>? Routes);

    private sealed record RutaOsrm(
        [property: JsonPropertyName("distance")] double Distance,
        [property: JsonPropertyName("duration")] double Duration,
        [property: JsonPropertyName("geometry")] GeometriaOsrm? Geometry);

    private sealed record GeometriaOsrm(
        [property: JsonPropertyName("coordinates")] List<double[]>? Coordinates);
}
