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

    /// <summary>
    /// HU-22: buscador de lugares compatible con Photon (OpenStreetMap). A diferencia de Nominatim, tolera
    /// palabras de más ("El Cerrito, Infonavit") y nombres incompletos.
    /// </summary>
    [Required, Url]
    public string UrlLugares { get; set; } = "https://photon.komoot.io";

    /// <summary>HU-22: solo se sugieren lugares a esta distancia del campus (Tula, Tepeji, Pachuca, CDMX, Querétaro).</summary>
    [Range(10, 500)]
    public double RadioLugaresKm { get; set; } = 150;

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
/// HU-08: geocodificación (Photon + Nominatim) y rutas por calles (OSRM) sobre OpenStreetMap. Los resultados se
/// guardan en memoria para no repetir consultas: Nominatim permite como máximo 1 petición por segundo.
/// HU-22: los lugares se buscan solo cerca del campus y se ordenan del más cercano al más lejano.
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

    /// <summary>Sugerencias que se devuelven como máximo.</summary>
    public const int MaximoLugares = 8;

    /// <summary>Partes de un texto que se intentan por separado como máximo (cada una cuesta dos consultas).</summary>
    private const int MaximoPartes = 3;
    private static readonly TimeSpan VigenciaRuta = TimeSpan.FromHours(6);

    /// <summary>
    /// Coordenadas del lugar más cercano al campus que coincide con el texto. Ya no se busca en todo México:
    /// un "Centro" o "El Cerrito" lejano dibujaba rutas de cientos de kilómetros.
    /// </summary>
    public async Task<PuntoGeo?> GeocodificarAsync(string direccion, PuntoGeo cercaDe, CancellationToken cancellationToken = default) =>
        (await BuscarLugaresAsync(direccion, cercaDe, cancellationToken)).FirstOrDefault()?.Punto;

    public async Task<IReadOnlyList<LugarGeo>> BuscarLugaresAsync(string texto, PuntoGeo cercaDe, CancellationToken cancellationToken = default)
    {
        var limpio = texto.Trim();
        if (limpio.Length == 0)
        {
            return [];
        }

        var clave = $"lugares:{limpio.ToLowerInvariant()}:{cercaDe.Latitud:F2},{cercaDe.Longitud:F2}";
        if (cache.TryGetValue(clave, out IReadOnlyList<LugarGeo>? guardados) && guardados is not null)
        {
            return guardados;
        }

        var (lugares, respondio) = await BuscarCercaAsync(limpio, cercaDe, cancellationToken);

        // "El Cerrito, Infonavit" o "Pachuca / Tula" no existen completos en el mapa: se intenta cada parte.
        if (lugares.Count == 0)
        {
            foreach (var parte in PartesDe(limpio).Take(MaximoPartes))
            {
                var (deLaParte, respondioParte) = await BuscarCercaAsync(parte, cercaDe, cancellationToken);
                lugares.AddRange(deLaParte);
                respondio |= respondioParte;
            }
        }

        if (!respondio)
        {
            // Ningún servicio respondió: no se guarda, para volver a intentarlo en la siguiente consulta.
            return [];
        }

        IReadOnlyList<LugarGeo> ordenados = lugares
            .DistinctBy(l => (l.Nombre.ToUpperInvariant(), Math.Round(l.Punto.Latitud, 3), Math.Round(l.Punto.Longitud, 3)))
            .OrderBy(l => DistanciaAproximada(l.Punto, cercaDe))
            .Take(MaximoLugares)
            .ToList();

        // Un texto sin resultados también se recuerda un rato, para no repetir consultas a los servicios.
        cache.Set(clave, ordenados, ordenados.Count > 0 ? VigenciaGeocodificacion : VigenciaSinResultados);
        return ordenados;
    }

    /// <summary>
    /// Busca el texto en Photon y en Nominatim y se queda con los lugares dentro del radio del campus.
    /// Devuelve también si al menos uno de los dos servicios respondió.
    /// </summary>
    private async Task<(List<LugarGeo> Lugares, bool Respondio)> BuscarCercaAsync(string texto, PuntoGeo cerca, CancellationToken ct)
    {
        var dePhoton = await ConsultarPhotonAsync(texto, cerca, ct);
        var deNominatim = await ConsultarNominatimAsync(texto, cerca, ct);

        var radio = opciones.Value.RadioLugaresKm;
        var cercanos = (dePhoton ?? [])
            .Concat(deNominatim ?? [])
            .Where(l => DistanciaKm(l.Punto, cerca) <= radio)
            .ToList();
        return (cercanos, dePhoton is not null || deNominatim is not null);
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

    /// <summary>Lugares de Photon cerca del campus; null si el servicio no respondió.</summary>
    private async Task<IReadOnlyList<LugarGeo>?> ConsultarPhotonAsync(string texto, PuntoGeo cerca, CancellationToken ct)
    {
        var radio = opciones.Value.RadioBusquedaGrados;
        var parametros = string.Create(
            CultureInfo.InvariantCulture,
            $"lat={cerca.Latitud:F5}&lon={cerca.Longitud:F5}&limit=10" +
            $"&bbox={cerca.Longitud - radio:F4},{cerca.Latitud - radio:F4},{cerca.Longitud + radio:F4},{cerca.Latitud + radio:F4}");
        var url = $"{opciones.Value.UrlLugares.TrimEnd('/')}/api/?{parametros}&q={Uri.EscapeDataString(texto)}";

        try
        {
            var respuesta = await http.GetFromJsonAsync<RespuestaPhoton>(url, ct);
            return (respuesta?.Features ?? [])
                .Select(f => ALugar(f))
                .OfType<LugarGeo>()
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "No se pudo consultar el buscador de lugares (Photon).");
            return null;
        }
    }

    private static LugarGeo? ALugar(LugarPhoton lugar)
    {
        if (lugar.Geometria?.Coordenadas is not { Count: >= 2 } c || lugar.Propiedades is not { } p
            || (p.CodigoPais is { } pais && !pais.Equals("MX", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var calle = string.Join(' ', new[] { p.Calle, p.Numero }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var partes = new[] { p.Nombre, calle, p.Colonia, p.Localidad, p.Ciudad, p.Municipio, p.Estado }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (partes.Count == 0)
        {
            return null;
        }

        return new LugarGeo(partes[0], string.Join(", ", partes), new PuntoGeo(c[1], c[0]));
    }

    /// <summary>Lugares de Nominatim dentro de la zona del campus; null si el servicio no respondió.</summary>
    private async Task<IReadOnlyList<LugarGeo>?> ConsultarNominatimAsync(string texto, PuntoGeo cerca, CancellationToken ct)
    {
        var radio = opciones.Value.RadioBusquedaGrados;
        var caja = string.Create(
            CultureInfo.InvariantCulture,
            $"{cerca.Longitud - radio:F4},{cerca.Latitud + radio:F4},{cerca.Longitud + radio:F4},{cerca.Latitud - radio:F4}");
        var url = $"{opciones.Value.UrlGeocodificacion.TrimEnd('/')}/search?format=jsonv2&limit=8&countrycodes=mx" +
                  $"&accept-language=es&viewbox={caja}&bounded=1&q={Uri.EscapeDataString(texto)}";

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

    /// <summary>Distancia en línea recta (Haversine), en km.</summary>
    private static double DistanciaKm(PuntoGeo a, PuntoGeo b)
    {
        const double radioTierraKm = 6371.0;
        static double Rad(double g) => g * Math.PI / 180.0;
        var dLat = Rad(b.Latitud - a.Latitud);
        var dLon = Rad(b.Longitud - a.Longitud);
        var h = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(Rad(a.Latitud)) * Math.Cos(Rad(b.Latitud)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * radioTierraKm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

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

    private sealed record RespuestaPhoton(
        [property: JsonPropertyName("features")] List<LugarPhoton>? Features);

    private sealed record LugarPhoton(
        [property: JsonPropertyName("geometry")] GeometriaPhoton? Geometria,
        [property: JsonPropertyName("properties")] PropiedadesPhoton? Propiedades);

    private sealed record GeometriaPhoton(
        [property: JsonPropertyName("coordinates")] List<double>? Coordenadas);

    private sealed record PropiedadesPhoton(
        [property: JsonPropertyName("name")] string? Nombre,
        [property: JsonPropertyName("street")] string? Calle,
        [property: JsonPropertyName("housenumber")] string? Numero,
        [property: JsonPropertyName("district")] string? Colonia,
        [property: JsonPropertyName("locality")] string? Localidad,
        [property: JsonPropertyName("city")] string? Ciudad,
        [property: JsonPropertyName("county")] string? Municipio,
        [property: JsonPropertyName("state")] string? Estado,
        [property: JsonPropertyName("countrycode")] string? CodigoPais);

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
