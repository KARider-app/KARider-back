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

/// <summary>HU-08: integración con Nominatim y OSRM (sin salir a internet).</summary>
public sealed class ServicioMapasOsmTests
{
    private static readonly PuntoGeo Campus = new(20.0875, -99.348);

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

    [Fact]
    public async Task Geocodifica_cerca_del_campus_y_guarda_el_resultado()
    {
        var (servicio, manejador) = Crear(_ => (HttpStatusCode.OK, """[{"lat":"20.0539","lon":"-99.3412"}]"""));

        var primero = await servicio.GeocodificarAsync("Tula Centro", Campus);
        var segundo = await servicio.GeocodificarAsync("  tula centro ", Campus);

        Assert.Equal(new PuntoGeo(20.0539, -99.3412), primero);
        Assert.Equal(primero, segundo);
        var url = Assert.Single(manejador.Urls);
        Assert.Contains("nominatim.openstreetmap.org/search", url);
        Assert.Contains("bounded=1", url);
        Assert.Contains("countrycodes=mx", url);
        Assert.Contains("q=Tula%20Centro", url);
    }

    [Fact]
    public async Task Si_no_hay_resultado_cerca_busca_en_todo_Mexico()
    {
        var (servicio, manejador) = Crear(url => url.Contains("bounded=1")
            ? (HttpStatusCode.OK, "[]")
            : (HttpStatusCode.OK, """[{"lat":"19.4326","lon":"-99.1332"}]"""));

        var punto = await servicio.GeocodificarAsync("Zócalo CDMX", Campus);

        Assert.Equal(new PuntoGeo(19.4326, -99.1332), punto);
        Assert.Equal(2, manejador.Urls.Count);
        Assert.Contains("bounded=0", manejador.Urls[1]);
    }

    [Fact]
    public async Task Sin_resultados_o_con_error_devuelve_null()
    {
        var (vacio, _) = Crear(_ => (HttpStatusCode.OK, "[]"));
        var (caido, _) = Crear(_ => (HttpStatusCode.ServiceUnavailable, ""));

        Assert.Null(await vacio.GeocodificarAsync("Nada", Campus));
        Assert.Null(await caido.GeocodificarAsync("Tula", Campus));
        Assert.Null(await vacio.GeocodificarAsync("   ", Campus));
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
