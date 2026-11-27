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
    private static readonly TimeSpan VigenciaSinResultados = TimeSpan.FromMinutes(30);

    /// <summary>Separadores con los que se escriben varios lugares en un mismo texto ("Pachuca / Tula").</summary>
    private static readonly string[] Separadores = ["/", ",", ";", "|", " - "];
    private static readonly TimeSpan VigenciaRuta = TimeSpan.FromHours(6);

    public async Task<PuntoGeo?> GeocodificarAsync(string direccion, PuntoGeo cercaDe, CancellationToken cancellationToken = default)
    {
        if ((await BuscarLugaresAsync(direccion, cercaDe, cancellationToken)).FirstOrDefault() is { } lugar)
        {
            return lugar.Punto;
        }

        // Textos como "Pachuca Plaza Galerías / Tula Centro" no existen completos en el mapa:
        // se intenta cada parte por separado y se usa la primera que se encuentre.
        foreach (var parte in PartesDe(direccion))
        {
            if ((await BuscarLugaresAsync(parte, cercaDe, cancellationToken)).FirstOrDefault() is { } encontrado)
            {
                return encontrado.Punto;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<LugarGeo>> BuscarLugaresAsync(string texto, PuntoGeo cercaDe, CancellationToken cancellationToken = default)
    {
        var limpio = texto.Trim();
        if (limpio.Length == 0)
        {
            return [];
        }

        var clave = $"geo:{limpio.ToLowerInvariant()}:{cercaDe.Latitud:F2},{cercaDe.Longitud:F2}";
        if (cache.TryGetValue(clave, out IReadOnlyList<LugarGeo>? guardados) && guardados is not null)
        {
            return guardados;
        }

        // Primero dentro de la zona del campus; si no hay resultado, en todo México.
        var lugares = await ConsultarNominatimAsync(limpio, cercaDe, acotado: true, cancellationToken);
        if (lugares is not { Count: > 0 })
        {
            lugares = await ConsultarNominatimAsync(limpio, cercaDe, acotado: false, cancellationToken);
        }

        if (lugares is null)
        {
            // El servicio no respondió: no se guarda, para volver a intentarlo en la siguiente consulta.
            return [];
        }

        // Un texto sin resultados también se recuerda un rato, para no repetir consultas a Nominatim.
        cache.Set(clave, lugares, lugares.Count > 0 ? VigenciaGeocodificacion : VigenciaSinResultados);
        return lugares;
    }

    /// <summary>Partes de un texto con varios lugares, sin repetir el texto completo.</summary>
    private static IEnumerable<string> PartesDe(string texto)
    {
        var partes = texto.Split(Separadores, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return partes.Length < 2
            ? []
            : partes.Where(p => p.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>Lugares encontrados, del más cercano al campus al más lejano; null si el servicio no respondió.</summary>
    private async Task<IReadOnlyList<LugarGeo>?> ConsultarNominatimAsync(string texto, PuntoGeo cerca, bool acotado, CancellationToken ct)
    {
        var radio = opciones.Value.RadioBusquedaGrados;
        var caja = string.Create(
            CultureInfo.InvariantCulture,
            $"{cerca.Longitud - radio:F4},{cerca.Latitud + radio:F4},{cerca.Longitud + radio:F4},{cerca.Latitud - radio:F4}");
        var url = $"{opciones.Value.UrlGeocodificacion.TrimEnd('/')}/search?format=jsonv2&limit=5&countrycodes=mx" +
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
            var resultados = await http.GetFromJsonAsync<List<ResultadoNominatim>>(url, ct) ?? [];

            // Entre los lugares con ese nombre va primero el más cercano al campus (p. ej. "Centro" o "Tula").
            return resultados
                .Select(r => ALugar(r, texto))
                .OfType<LugarGeo>()
                .DistinctBy(l => (l.Direccion.ToUpperInvariant(), Math.Round(l.Punto.Latitud, 4), Math.Round(l.Punto.Longitud, 4)))
                .OrderBy(l => DistanciaAproximada(l.Punto, cerca))
                .ToList();
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

    private static LugarGeo? ALugar(ResultadoNominatim r, string textoBuscado)
    {
        if (!double.TryParse(r.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(r.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            return null;
        }

        // "Centro, Tula, Tula de Allende, Hidalgo, 42800, México" se muestra sin el país.
        var direccion = (r.DisplayName ?? string.Empty).Trim();
        if (direccion.EndsWith(SufijoPais, StringComparison.OrdinalIgnoreCase))
        {
            direccion = direccion[..^SufijoPais.Length];
        }

        var nombre = !string.IsNullOrWhiteSpace(r.Name) ? r.Name.Trim() : direccion.Split(',')[0].Trim();
        if (nombre.Length == 0)
        {
            nombre = textoBuscado;
        }

        return new LugarGeo(nombre, direccion.Length > 0 ? direccion : nombre, new PuntoGeo(lat, lon));
    }

    private const string SufijoPais = ", México";

    /// <summary>Distancia al cuadrado en grados, corregida por latitud: suficiente para ordenar por cercanía.</summary>
    private static double DistanciaAproximada(PuntoGeo a, PuntoGeo b)
    {
        var dLat = a.Latitud - b.Latitud;
        var dLon = (a.Longitud - b.Longitud) * Math.Cos(b.Latitud * Math.PI / 180.0);
        return dLat * dLat + dLon * dLon;
    }

    private sealed record ResultadoNominatim(
        [property: JsonPropertyName("lat")] string? Lat,
        [property: JsonPropertyName("lon")] string? Lon,
        [property: JsonPropertyName("name")] string? Name = null,
        [property: JsonPropertyName("display_name")] string? DisplayName = null);

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
