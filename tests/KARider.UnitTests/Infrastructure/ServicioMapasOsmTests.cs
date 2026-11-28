using System.Net;
using System.Text;
using KARider.Application.Abstractions;
using KARider.Infrastructure.Maps;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KARider.UnitTests.Infrastructure;

/// <summary>Responde según la URL pedida y guarda las URLs consultadas.</summary>
internal sealed class ManejadorFalso(Func<string, (HttpStatusCode Estado, string Cuerpo)> responder) : HttpMessageHandler
{
    public List<string> Urls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.AbsoluteUri;
        Urls.Add(url);
        var (estado, cuerpo) = responder(url);
        return Task.FromResult(new HttpResponseMessage(estado)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
        });
    }
}

/// <summary>HU-08 y HU-22: integración con Photon, Nominatim y OSRM (sin salir a internet).</summary>
public sealed class ServicioMapasOsmTests
{
    private static readonly PuntoGeo Campus = new(20.0875, -99.348);

    private const string SinLugaresPhoton = """{"type":"FeatureCollection","features":[]}""";

    private const string RutaOsrm = """
        {"code":"Ok","routes":[{"distance":6140.5,"duration":504,
          "geometry":{"type":"LineString","coordinates":[[-99.348,20.0875],[-99.345,20.07],[-99.3416,20.0539]]}}]}
        """;

    private static (ServicioMapasOsm Servicio, ManejadorFalso Manejador) Crear(Func<string, (HttpStatusCode, string)> responder)
    {
        var manejador = new ManejadorFalso(responder);
        var opciones = Options.Create(new MapasOptions { SegundosEntreConsultas = 0 });
        var servicio = new ServicioMapasOsm(
            new HttpClient(manejador), new MemoryCache(new MemoryCacheOptions()), opciones, NullLogger<ServicioMapasOsm>.Instance);
        return (servicio, manejador);
    }

    /// <summary>Responde distinto a Photon y a Nominatim según el texto buscado.</summary>
    private static Func<string, (HttpStatusCode, string)> Servicios(
        Func<string, string>? photon = null,
        Func<string, string>? nominatim = null) =>
        url => url.Contains("photon.komoot.io")
            ? (HttpStatusCode.OK, photon?.Invoke(url) ?? SinLugaresPhoton)
            : (HttpStatusCode.OK, nominatim?.Invoke(url) ?? "[]");

    private static string LugarPhoton(string nombre, double lat, double lon, string ciudad = "Tula de Allende", string pais = "MX")
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        return "{\"geometry\":{\"type\":\"Point\",\"coordinates\":[" + lon.ToString(c) + "," + lat.ToString(c) + "]}," +
               "\"properties\":{\"name\":\"" + nombre + "\",\"city\":\"" + ciudad + "\",\"county\":\"" + ciudad +
               "\",\"state\":\"Hidalgo\",\"countrycode\":\"" + pais + "\"}}";
    }

    private static string Photon(params string[] lugares) =>
        "{\"type\":\"FeatureCollection\",\"features\":[" + string.Join(',', lugares) + "]}";

    [Fact]
    public async Task Geocodifica_cerca_del_campus_y_guarda_el_resultado()
    {
        var (servicio, manejador) = Crear(Servicios(nominatim: _ => """[{"lat":"20.0539","lon":"-99.3412"}]"""));

        var primero = await servicio.GeocodificarAsync("Tula Centro", Campus);
        var segundo = await servicio.GeocodificarAsync("  tula centro ", Campus);

        Assert.Equal(new PuntoGeo(20.0539, -99.3412), primero);
        Assert.Equal(primero, segundo);
        // Una consulta a cada buscador; la segunda vez sale de la memoria.
        Assert.Equal(2, manejador.Urls.Count);
        var photon = manejador.Urls.Single(u => u.Contains("photon"));
        Assert.Contains("photon.komoot.io/api/", photon);
        Assert.Contains("lat=20.08750&lon=-99.34800", photon);
        Assert.Contains("bbox=-100.3480,19.0875,-98.3480,21.0875", photon);
        var nominatim = manejador.Urls.Single(u => u.Contains("nominatim"));
        Assert.Contains("bounded=1", nominatim);
        Assert.Contains("countrycodes=mx", nominatim);
        Assert.Contains("q=Tula%20Centro", nominatim);
    }

    [Fact]
    public async Task Entre_varios_lugares_con_el_mismo_nombre_elige_el_mas_cercano_al_campus()
    {
        // "Centro": el primer resultado está en Pachuca; el segundo es el centro de Tula, junto al campus.
        var (servicio, _) = Crear(Servicios(nominatim: _ =>
            """[{"lat":"20.1170","lon":"-98.7330"},{"lat":"x","lon":"y"},{"lat":"20.0539","lon":"-99.3416"}]"""));

        var punto = await servicio.GeocodificarAsync("Centro", Campus);

        Assert.Equal(new PuntoGeo(20.0539, -99.3416), punto);
    }

    [Fact]
    public async Task HU22_ya_no_busca_en_todo_Mexico_ni_acepta_lugares_lejanos()
    {
        // Photon encontró un "El Cerrito" en Nuevo León (a más de 500 km): no se usa.
        var (servicio, manejador) = Crear(Servicios(photon: _ => Photon(LugarPhoton("Parque Infonavit El Cerrito", 24.8649, -99.5492, "Linares"))));

        Assert.Null(await servicio.GeocodificarAsync("Infonavit El Cerrito", Campus));
        Assert.DoesNotContain(manejador.Urls, u => u.Contains("bounded=0"));
    }

    [Fact]
    public async Task HU22_photon_encuentra_textos_con_palabras_de_mas_y_se_combina_con_nominatim()
    {
        var (servicio, _) = Crear(Servicios(
            photon: _ => Photon(
                LugarPhoton("El Cerrito", 20.010278, -99.351667, "San Marcos"),
                LugarPhoton("Calle El Cerrito", 19.9928823, -99.3291407),
                LugarPhoton("Lugar en Guatemala", 20.05, -99.35, pais: "GT")),
            nominatim: _ => """[{"lat":"20.0539","lon":"-99.3412","name":"El Cerrito","display_name":"El Cerrito, Tula, Hidalgo, México"}]"""));

        var lugares = await servicio.BuscarLugaresAsync("El Cerrito Infonavit", Campus);

        Assert.Equal(3, lugares.Count);
        // Del más cercano al campus al más lejano.
        Assert.Equal(["El Cerrito", "El Cerrito", "Calle El Cerrito"], lugares.Select(l => l.Nombre));
        Assert.Equal(new PuntoGeo(20.0539, -99.3412), lugares[0].Punto);
        Assert.Equal("El Cerrito, San Marcos, Hidalgo", lugares[1].Direccion);
    }

    [Fact]
    public async Task HU22_si_el_texto_completo_no_existe_busca_cada_parte()
    {
        var (servicio, manejador) = Crear(Servicios(
            photon: url => url.Contains("q=El%20Cerrito&") || url.EndsWith("q=El%20Cerrito", StringComparison.Ordinal)
                ? Photon(LugarPhoton("El Cerrito", 20.010278, -99.351667, "San Marcos"))
                : SinLugaresPhoton));

        var lugares = await servicio.BuscarLugaresAsync("El Cerrito, Infonavit", Campus);
        var punto = await servicio.GeocodificarAsync("El Cerrito, Infonavit", Campus);

        Assert.Equal("El Cerrito", Assert.Single(lugares).Nombre);
        Assert.Equal(new PuntoGeo(20.010278, -99.351667), punto);
        // Texto completo y sus dos partes, en los dos buscadores; la segunda vez sale de la memoria.
        Assert.Equal(6, manejador.Urls.Count);
    }

    [Fact]
    public async Task HU22_busca_lugares_con_nombre_y_direccion_sin_duplicados()
    {
        var (servicio, manejador) = Crear(Servicios(nominatim: _ => """
            [
              {"lat":"20.1170","lon":"-98.7330","name":"Centro","display_name":"Centro, Pachuca de Soto, Hidalgo, 42000, México"},
              {"lat":"20.0539","lon":"-99.3416","name":"","display_name":"Centro, Tula, Tula de Allende, Hidalgo, 42800, México"},
              {"lat":"20.0539","lon":"-99.3416","name":"Centro","display_name":"Centro, Tula, Tula de Allende, Hidalgo, 42800, México"},
              {"lat":"x","lon":"-99.3"}
            ]
            """));

        var lugares = await servicio.BuscarLugaresAsync("Centro", Campus);
        await servicio.BuscarLugaresAsync(" CENTRO ", Campus);

        Assert.Equal(2, lugares.Count);
        Assert.Equal(new LugarGeo("Centro", "Centro, Tula, Tula de Allende, Hidalgo, 42800", new PuntoGeo(20.0539, -99.3416)), lugares[0]);
        Assert.Equal("Centro, Pachuca de Soto, Hidalgo, 42000", lugares[1].Direccion);
        Assert.Equal(2, manejador.Urls.Count);
    }

    [Fact]
    public async Task HU22_si_un_buscador_falla_usa_el_otro()
    {
        var (servicio, _) = Crear(url => url.Contains("photon")
            ? (HttpStatusCode.ServiceUnavailable, "")
            : (HttpStatusCode.OK, """[{"lat":"20.0539","lon":"-99.3412","display_name":"Tula, Hidalgo, México"}]"""));

        var lugar = Assert.Single(await servicio.BuscarLugaresAsync("Tula", Campus));

        Assert.Equal(new LugarGeo("Tula", "Tula, Hidalgo", new PuntoGeo(20.0539, -99.3412)), lugar);
    }

    [Fact]
    public async Task HU22_si_ningun_servicio_responde_no_guarda_y_reintenta()
    {
        var falla = true;
        var (servicio, manejador) = Crear(url => falla
            ? (HttpStatusCode.ServiceUnavailable, "")
            : Servicios(nominatim: _ => """[{"lat":"20.0539","lon":"-99.3412","display_name":"Tula, Hidalgo, México"}]""")(url));

        Assert.Empty(await servicio.BuscarLugaresAsync("Tula", Campus));
        falla = false;
        Assert.Single(await servicio.BuscarLugaresAsync("Tula", Campus));

        Assert.Equal(4, manejador.Urls.Count);
    }

    [Fact]
    public async Task Sin_resultados_o_con_error_devuelve_null_y_recuerda_los_textos_sin_resultados()
    {
        var (vacio, manejadorVacio) = Crear(Servicios());
        var (caido, _) = Crear(_ => (HttpStatusCode.ServiceUnavailable, ""));

        Assert.Null(await vacio.GeocodificarAsync("Nada", Campus));
        Assert.Null(await vacio.GeocodificarAsync("Nada", Campus));
        Assert.Null(await caido.GeocodificarAsync("Tula", Campus));
        Assert.Null(await vacio.GeocodificarAsync("   ", Campus));
        Assert.Equal(2, manejadorVacio.Urls.Count);
    }

    [Fact]
    public async Task Calcula_la_ruta_por_calles_con_OSRM()
    {
        var (servicio, manejador) = Crear(_ => (HttpStatusCode.OK, RutaOsrm));

        var ruta = await servicio.CalcularRutaAsync([Campus, new PuntoGeo(20.0539, -99.3416)]);
        await servicio.CalcularRutaAsync([Campus, new PuntoGeo(20.0539, -99.3416)]);

        Assert.NotNull(ruta);
        Assert.Equal(6.1405, ruta.DistanciaKm, 4);
        Assert.Equal(8.4, ruta.DuracionMinutos, 1);
        // OSRM entrega [longitud, latitud]; el servicio lo convierte a (latitud, longitud).
        Assert.Equal(new PuntoGeo(20.07, -99.345), ruta.Trazo[1]);
        var url = Assert.Single(manejador.Urls);
        Assert.Contains("/route/v1/driving/-99.34800,20.08750;-99.34160,20.05390", url);
        Assert.Contains("geometries=geojson", url);
    }

    [Fact]
    public async Task Si_OSRM_falla_o_no_encuentra_ruta_devuelve_null()
    {
        var (sinRuta, _) = Crear(_ => (HttpStatusCode.OK, """{"code":"NoRoute","routes":[]}"""));
        var (caido, _) = Crear(_ => (HttpStatusCode.InternalServerError, ""));
        var (invalido, _) = Crear(_ => (HttpStatusCode.OK, "no es json"));
        PuntoGeo[] puntos = [Campus, new PuntoGeo(20.05, -99.34)];

        Assert.Null(await sinRuta.CalcularRutaAsync(puntos));
        Assert.Null(await caido.CalcularRutaAsync(puntos));
        Assert.Null(await invalido.CalcularRutaAsync(puntos));
        Assert.Null(await sinRuta.CalcularRutaAsync([Campus]));
    }
}
