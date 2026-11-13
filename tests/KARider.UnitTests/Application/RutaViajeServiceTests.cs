using KARider.Application.Abstractions;
using KARider.Application.Features.Viajes;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using KARider.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KARider.UnitTests.Application;

/// <summary>Servicio de mapas falso: responde con datos fijos y cuenta las llamadas.</summary>
internal sealed class MapasFalsos : IServicioMapas
{
    public Dictionary<string, PuntoGeo> Lugares { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool RutasDisponibles { get; set; } = true;

    public int Geocodificaciones { get; private set; }

    public List<IReadOnlyList<PuntoGeo>> RutasPedidas { get; } = [];

    public Task<PuntoGeo?> GeocodificarAsync(string direccion, PuntoGeo cercaDe, CancellationToken cancellationToken = default)
    {
        Geocodificaciones++;
        return Task.FromResult(Lugares.GetValueOrDefault(direccion));
    }

    public Task<RutaCalculada?> CalcularRutaAsync(IReadOnlyList<PuntoGeo> puntos, CancellationToken cancellationToken = default)
    {
        RutasPedidas.Add(puntos);
        if (!RutasDisponibles)
        {
            return Task.FromResult<RutaCalculada?>(null);
        }

        // Trazo "por calles": los puntos pedidos con un vértice intermedio, como haría OSRM.
        var trazo = new List<PuntoGeo> { puntos[0], new(puntos[0].Latitud + 0.001, puntos[0].Longitud) };
        trazo.AddRange(puntos.Skip(1));
        return Task.FromResult<RutaCalculada?>(new RutaCalculada(trazo, 6.14, 8.4));
    }
}

/// <summary>HU-08: ruta real del viaje y llegada del conductor.</summary>
public sealed class RutaViajeServiceTests
{
    private static readonly PuntoGeo Tula = new(20.0539, -99.3412);
    private static readonly PuntoGeo Tepeji = new(19.9046, -99.3443);

    private readonly string _baseDatos = Guid.NewGuid().ToString();
    private readonly MapasFalsos _mapas = new();
    private readonly DateTimeOffset _ahora = DateTimeOffset.UtcNow;

    private Guid _conductorId;
    private Guid _pasajeroId;
    private Guid _viajeId;

    public RutaViajeServiceTests()
    {
        _mapas.Lugares["Tula Centro"] = Tula;
        _mapas.Lugares["Tepeji"] = Tepeji;
    }

    private AppDbContext NuevoContexto() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(_baseDatos)
        .Options);

    private RutaViajeService Servicio(AppDbContext db) => new(db, _mapas);

    private async Task SembrarAsync(
        string destino = "Tula Centro",
        double? destinoLatitud = null,
        double? destinoLongitud = null,
        string[]? paradas = null,
        bool publicar = true,
        bool conReserva = true,
        Action<Viaje>? preparar = null)
    {
        await using var db = NuevoContexto();
        db.Carreras.Add(new Carrera { Id = 1, Nombre = "ISC" });
        db.PuntosEncuentro.Add(new PuntoEncuentro
        {
            Id = 1,
            Nombre = "Puerta Principal",
            Descripcion = "Caseta",
            Latitud = 20.0875,
            Longitud = -99.348,
            Activo = true,
        });

        var conductor = Usuario.Crear("Conductor", "22300001", "7731234567", "c@uttt.edu.mx", "hash", Rol.Conductor, 1, 1);
        var pasajero = Usuario.Crear("Pasajero", "22300002", "7731234568", "p@uttt.edu.mx", "hash", Rol.Pasajero, 1, 1);
        db.Usuarios.AddRange(conductor, pasajero);
        _conductorId = conductor.Id;
        _pasajeroId = pasajero.Id;

        var vehiculo = Vehiculo.Crear(conductor.Id, "Versa", "Azul", 2022, "HNX-421-C", 4);
        db.Vehiculos.Add(vehiculo);

        var viaje = Viaje.Crear(conductor.Id, vehiculo, 1, destino, destinoLatitud, destinoLongitud,
            _ahora.AddHours(2), 3, 160m, null, paradas ?? [], publicar, _ahora).Value;
        db.Viajes.Add(viaje);
        _viajeId = viaje.Id;

        if (conReserva && publicar)
        {
            db.Reservas.Add(Reserva.Crear(viaje, pasajero.Id, 1, _ahora).Value);
        }

        await db.SaveChangesAsync();
        if (preparar is not null)
        {
            preparar(viaje);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task La_ruta_va_por_calles_y_guarda_las_coordenadas_del_destino_geocodificado()
    {
        await SembrarAsync(paradas: ["Tepeji", "Lugar inexistente"]);

        await using (var db = NuevoContexto())
        {
            var ruta = (await Servicio(db).ObtenerRutaAsync(_pasajeroId, _viajeId, default)).Value;

            Assert.True(ruta.PorCalles);
            Assert.Equal("Puerta Principal", ruta.Origen.Nombre);
            Assert.Equal(new PuntoRutaDto(Tula.Latitud, Tula.Longitud, "Tula Centro"), ruta.Destino);
            // La parada que no se encontró se omite; la otra se usa como punto intermedio.
            Assert.Equal(["Tepeji"], ruta.Paradas.Select(p => p.Nombre));
            Assert.Equal(3, _mapas.RutasPedidas.Single().Count);
            Assert.Equal(4, ruta.Trazo.Count);
            Assert.Equal(6.1, ruta.DistanciaKm);
            Assert.Equal(8, ruta.DuracionMinutos);
        }

        await using (var db = NuevoContexto())
        {
            var viaje = await db.Viajes.SingleAsync(v => v.Id == _viajeId);
            Assert.Equal(Tula.Latitud, viaje.DestinoLatitud);
            Assert.Equal(Tula.Longitud, viaje.DestinoLongitud);
        }
    }

    [Fact]
    public async Task Si_el_destino_ya_tiene_coordenadas_no_se_geocodifica()
    {
        await SembrarAsync(destino: "Mi casa", destinoLatitud: 20.1, destinoLongitud: -99.2);
        await using var db = NuevoContexto();

        var ruta = (await Servicio(db).ObtenerRutaAsync(_pasajeroId, _viajeId, default)).Value;

        Assert.Equal(0, _mapas.Geocodificaciones);
        Assert.Equal(new PuntoRutaDto(20.1, -99.2, "Mi casa"), ruta.Destino);
    }

    [Fact]
    public async Task Si_el_servicio_de_rutas_falla_se_dibuja_una_linea_recta_de_respaldo()
    {
        _mapas.RutasDisponibles = false;
        await SembrarAsync();
        await using var db = NuevoContexto();

        var ruta = (await Servicio(db).ObtenerRutaAsync(_pasajeroId, _viajeId, default)).Value;

        Assert.False(ruta.PorCalles);
        Assert.Equal(2, ruta.Trazo.Count);
        Assert.InRange(ruta.DistanciaKm!.Value, 3.5, 4.5);
        Assert.True(ruta.DuracionMinutos > 0);
    }

    [Fact]
    public async Task Si_el_destino_no_se_puede_ubicar_solo_se_muestra_el_punto_de_encuentro()
    {
        await SembrarAsync(destino: "Destino desconocido");
        await using var db = NuevoContexto();

        var ruta = (await Servicio(db).ObtenerRutaAsync(_pasajeroId, _viajeId, default)).Value;

        Assert.Null(ruta.Destino);
        Assert.Empty(ruta.Trazo);
        Assert.Null(ruta.DistanciaKm);
        Assert.Empty(_mapas.RutasPedidas);
    }

    [Fact]
    public async Task Un_borrador_solo_lo_ve_su_conductor()
    {
        await SembrarAsync(publicar: false);
        await using var db = NuevoContexto();
        var servicio = Servicio(db);

        Assert.Equal(DomainErrors.Viaje.NoEncontrado, (await servicio.ObtenerRutaAsync(_pasajeroId, _viajeId, default)).Error);
        Assert.True((await servicio.ObtenerRutaAsync(_conductorId, _viajeId, default)).IsSuccess);
    }

    [Fact]
    public async Task Criterio1_con_el_viaje_programado_la_llegada_es_al_punto_de_encuentro()
    {
        await SembrarAsync(preparar: v => v.ActualizarUbicacion(20.06, -99.34, _ahora));
        await using var db = NuevoContexto();

        var llegada = (await Servicio(db).ObtenerLlegadaAsync(_pasajeroId, _viajeId, default)).Value;

        Assert.Equal(ObjetivoLlegada.PuntoEncuentro, llegada.Objetivo);
        Assert.True(llegada.PorCalles);
        Assert.Equal(6.1, llegada.DistanciaKm);
        Assert.Equal(8, llegada.DuracionMinutos);
        var pedida = _mapas.RutasPedidas.Single();
        Assert.Equal(new PuntoGeo(20.06, -99.34), pedida[0]);
        Assert.Equal(new PuntoGeo(20.0875, -99.348), pedida[1]);
    }

    [Fact]
    public async Task Con_el_viaje_en_curso_la_llegada_es_al_destino()
    {
        await SembrarAsync(preparar: v =>
        {
            v.CambiarEstado(EstadoViaje.EnCurso, _ahora);
            v.ActualizarUbicacion(20.07, -99.345, _ahora);
        });
        await using var db = NuevoContexto();

        var llegada = (await Servicio(db).ObtenerLlegadaAsync(_pasajeroId, _viajeId, default)).Value;

        Assert.Equal(ObjetivoLlegada.Destino, llegada.Objetivo);
        Assert.Equal(Tula, _mapas.RutasPedidas.Single()[1]);
    }

    [Fact]
    public async Task Criterio2_sin_ubicacion_del_conductor_se_espera()
    {
        await SembrarAsync();
        await using var db = NuevoContexto();

        var resultado = await Servicio(db).ObtenerLlegadaAsync(_pasajeroId, _viajeId, default);

        Assert.Equal(DomainErrors.Viaje.UbicacionNoDisponible, resultado.Error);
    }

    [Fact]
    public async Task Regla1_al_completar_el_viaje_se_deja_de_dar_la_ubicacion()
    {
        await SembrarAsync(preparar: v =>
        {
            v.ActualizarUbicacion(20.07, -99.345, _ahora);
            v.CambiarEstado(EstadoViaje.EnCurso, _ahora);
            v.CambiarEstado(EstadoViaje.Completado, _ahora);
        });
        await using var db = NuevoContexto();

        var resultado = await Servicio(db).ObtenerLlegadaAsync(_conductorId, _viajeId, default);

        Assert.Equal(DomainErrors.Viaje.UbicacionNoDisponible, resultado.Error);
    }

    [Fact]
    public async Task Regla2_solo_pasajeros_confirmados_consultan_la_llegada()
    {
        await SembrarAsync(conReserva: false, preparar: v => v.ActualizarUbicacion(20.06, -99.34, _ahora));
        await using var db = NuevoContexto();

        var resultado = await Servicio(db).ObtenerLlegadaAsync(_pasajeroId, _viajeId, default);

        Assert.Equal(DomainErrors.Viaje.AccesoDenegado, resultado.Error);
    }

    [Fact]
    public async Task Un_viaje_inexistente_responde_no_encontrado()
    {
        await using var db = NuevoContexto();
        var servicio = Servicio(db);

        Assert.Equal(DomainErrors.Viaje.NoEncontrado, (await servicio.ObtenerRutaAsync(Guid.NewGuid(), Guid.NewGuid(), default)).Error);
        Assert.Equal(DomainErrors.Viaje.NoEncontrado, (await servicio.ObtenerLlegadaAsync(Guid.NewGuid(), Guid.NewGuid(), default)).Error);
    }

    [Fact]
    public void El_dominio_no_pisa_coordenadas_de_destino_existentes()
    {
        var conductor = Usuario.Crear("C", "22300009", "7731234569", "x@uttt.edu.mx", "hash", Rol.Conductor, 1, 1);
        var vehiculo = Vehiculo.Crear(conductor.Id, "Versa", "Azul", 2022, "HNX-421-C", 4);
        var viaje = Viaje.Crear(conductor.Id, vehiculo, 1, "Tula", 1, 2, _ahora.AddHours(2), 3, 160m, null, [], true, _ahora).Value;

        Assert.False(viaje.CompletarCoordenadasDestino(20, -99));
        Assert.Equal(1, viaje.DestinoLatitud);
    }
}
