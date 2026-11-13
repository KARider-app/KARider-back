using KARider.Application.Common;
using KARider.Application.Features.Perfil;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KARider.UnitTests.Application;

/// <summary>HU-10: historial de viajes y estadísticas del perfil.</summary>
public sealed class PerfilServiceTests
{
    private static readonly EstadisticasOptions Opciones = new();
    private readonly string _baseDatos = Guid.NewGuid().ToString();
    private readonly DateTimeOffset _ahora = DateTimeOffset.UtcNow;

    private Guid _usuarioId;
    private Usuario _otroConductor = null!;
    private Usuario _otroPasajero = null!;
    private Usuario _usuario = null!;
    private Vehiculo _vehiculoUsuario = null!;
    private Vehiculo _vehiculoOtro = null!;

    private AppDbContext NuevoContexto() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(_baseDatos)
        .Options);

    private PerfilService Servicio(AppDbContext db) => new(db, Options.Create(Opciones));

    private void SembrarCatalogos(AppDbContext db)
    {
        db.Carreras.Add(new Carrera { Id = 1, Nombre = "ISC" });
        db.PuntosEncuentro.Add(new PuntoEncuentro
        {
            Id = 1,
            Nombre = "Entrada UTTT",
            Descripcion = "Caseta",
            Latitud = 20.0539,
            Longitud = -99.3412,
            Activo = true,
        });

        _usuario = Usuario.Crear("Kazumi Barrera", "22300010", "7731234500", "kazumi@uttt.edu.mx", "hash", Rol.Conductor, 1, 1);
        _otroConductor = Usuario.Crear("Ana Conductora", "22300011", "7731234501", "ana@uttt.edu.mx", "hash", Rol.Conductor, 1, 1);
        _otroPasajero = Usuario.Crear("Luis Pasajero", "22300012", "7731234502", "luis@uttt.edu.mx", "hash", Rol.Pasajero, 1, 1);
        db.Usuarios.AddRange(_usuario, _otroConductor, _otroPasajero);
        _usuarioId = _usuario.Id;

        _vehiculoUsuario = Vehiculo.Crear(_usuario.Id, "Nissan Versa", "Azul", 2022, "HNX-421-C", 4);
        _vehiculoOtro = Vehiculo.Crear(_otroConductor.Id, "Kia Rio", "Rojo", 2021, "HNX-100-A", 4);
        db.Vehiculos.AddRange(_vehiculoUsuario, _vehiculoOtro);
    }

    private Viaje NuevoViaje(AppDbContext db, Usuario conductor, Vehiculo vehiculo, int horas, string destino,
        double? destinoLatitud = null, double? destinoLongitud = null)
    {
        var viaje = Viaje.Crear(
            conductor.Id, vehiculo, 1, destino, destinoLatitud, destinoLongitud,
            _ahora.AddHours(horas), 3, 120m, null, [], publicar: true, _ahora).Value;
        db.Viajes.Add(viaje);
        return viaje;
    }

    /// <summary>HU-07: con cupo disponible la reserva queda confirmada de inmediato.</summary>
    private Reserva Reservar(AppDbContext db, Viaje viaje, Usuario pasajero)
    {
        var reserva = Reserva.Crear(viaje, pasajero.Id, 1, _ahora).Value;
        db.Reservas.Add(reserva);
        return reserva;
    }

    private static void Completar(Viaje viaje, DateTimeOffset ahora)
    {
        Assert.True(viaje.CambiarEstado(EstadoViaje.EnCurso, ahora).IsSuccess);
        Assert.True(viaje.CambiarEstado(EstadoViaje.Completado, ahora).IsSuccess);
    }

    /// <summary>Escenario del criterio 1: 2 viajes como pasajero y 1 como conductor, más ruido que no debe listarse.</summary>
    private async Task SembrarEscenarioAsync()
    {
        await using var db = NuevoContexto();
        SembrarCatalogos(db);

        // Como pasajero (2 completados) en viajes de otra conductora.
        var p1 = NuevoViaje(db, _otroConductor, _vehiculoOtro, 1, "Tula Centro");
        Reservar(db, p1, _usuario);
        var p2 = NuevoViaje(db, _otroConductor, _vehiculoOtro, 3, "Pachuca", 20.1011, -98.7591);
        Reservar(db, p2, _usuario);

        // Como conductor (1 completado) llevando a Luis.
        var c1 = NuevoViaje(db, _usuario, _vehiculoUsuario, 2, "Tepeji");
        Reservar(db, c1, _otroPasajero);

        await db.SaveChangesAsync();
        Completar(p1, _ahora);
        Completar(p2, _ahora);
        Completar(c1, _ahora);

        // Ruido: un viaje propio y una reserva en viajes aún programados todavía no son historial.
        NuevoViaje(db, _usuario, _vehiculoUsuario, 30, "Futuro programado");
        var futuro = NuevoViaje(db, _otroConductor, _vehiculoOtro, 40, "Reserva futura");
        Reservar(db, futuro, _usuario);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Todos_lista_los_tres_viajes_del_mas_reciente_al_mas_antiguo_y_cuenta_tres_viajes_hechos()
    {
        await SembrarEscenarioAsync();
        await using var db = NuevoContexto();
        var servicio = Servicio(db);

        var historial = await servicio.ObtenerHistorialAsync(_usuarioId, new HistorialQuery(), default);
        var perfil = await servicio.ObtenerAsync(_usuarioId, default);

        Assert.Equal(3, historial.Total);
        Assert.Equal(["Pachuca", "Tepeji", "Tula Centro"], historial.Items.Select(i => i.Destino));
        Assert.All(historial.Items, i => Assert.Equal("Entrada UTTT", i.Origen));
        Assert.Equal(3, perfil.Value.Estadisticas.ViajesRealizados);
        Assert.Equal(1, perfil.Value.Estadisticas.ViajesComoConductor);
        Assert.Equal(2, perfil.Value.Estadisticas.ViajesComoPasajero);
    }

    [Fact]
    public async Task El_filtro_Pasajero_oculta_los_viajes_publicados_como_conductor()
    {
        await SembrarEscenarioAsync();
        await using var db = NuevoContexto();

        var historial = await Servicio(db).ObtenerHistorialAsync(_usuarioId, new HistorialQuery(FiltroRolHistorial.Pasajero), default);

        Assert.Equal(2, historial.Total);
        Assert.All(historial.Items, i =>
        {
            Assert.Equal(Rol.Pasajero, i.RolEnViaje);
            Assert.Equal("Ana Conductora", i.Companero!.NombreCompleto);
            Assert.Equal("Kia Rio (Rojo)", i.Companero.Vehiculo);
            Assert.Equal(30m, i.Monto);
        });
    }

    [Fact]
    public async Task El_filtro_Conductor_muestra_a_los_pasajeros_que_llevo()
    {
        await SembrarEscenarioAsync();
        await using var db = NuevoContexto();

        var historial = await Servicio(db).ObtenerHistorialAsync(_usuarioId, new HistorialQuery(FiltroRolHistorial.Conductor), default);

        var item = Assert.Single(historial.Items);
        Assert.Equal(Rol.Conductor, item.RolEnViaje);
        Assert.Equal(EstadoViaje.Completado, item.EstadoViaje);
        Assert.Equal(1, item.Pasajeros);
        Assert.Equal(30m, item.Monto);
        Assert.Equal(["Luis Pasajero"], item.NombresPasajeros);
    }

    [Fact]
    public async Task Sin_viajes_el_historial_queda_vacio_y_las_estadisticas_en_cero()
    {
        await using (var semilla = NuevoContexto())
        {
            SembrarCatalogos(semilla);
            await semilla.SaveChangesAsync();
        }

        await using var db = NuevoContexto();
        var servicio = Servicio(db);

        var historial = await servicio.ObtenerHistorialAsync(_usuarioId, new HistorialQuery(), default);
        var estadisticas = (await servicio.ObtenerAsync(_usuarioId, default)).Value.Estadisticas;

        Assert.Empty(historial.Items);
        Assert.Equal(0, historial.Total);
        Assert.Equal(new EstadisticasDto(0, 0, 0, 0m, 0m), estadisticas);
    }

    [Fact]
    public async Task Los_viajes_cancelados_se_listan_con_su_estado_y_no_suman_estadisticas()
    {
        await using (var semilla = NuevoContexto())
        {
            SembrarCatalogos(semilla);
            var cancelado = NuevoViaje(semilla, _otroConductor, _vehiculoOtro, 5, "Viaje cancelado");
            Reservar(semilla, cancelado, _usuario);
            await semilla.SaveChangesAsync();
            Assert.True(cancelado.CambiarEstado(EstadoViaje.Cancelado, _ahora).IsSuccess);
            await semilla.SaveChangesAsync();
        }

        await using var db = NuevoContexto();
        var servicio = Servicio(db);

        var item = Assert.Single((await servicio.ObtenerHistorialAsync(_usuarioId, new HistorialQuery(), default)).Items);
        var estadisticas = (await servicio.ObtenerAsync(_usuarioId, default)).Value.Estadisticas;

        Assert.Equal(EstadoViaje.Cancelado, item.EstadoViaje);
        Assert.Equal(EstadoReserva.Cancelada, item.EstadoReserva);
        Assert.Equal(0, estadisticas.ViajesRealizados);
    }

    [Fact]
    public async Task Las_estadisticas_usan_la_formula_documentada()
    {
        await SembrarEscenarioAsync();
        await using var db = NuevoContexto();

        var estadisticas = (await Servicio(db).ObtenerAsync(_usuarioId, default)).Value.Estadisticas;

        // Tula Centro y Tepeji no tienen coordenadas: se usa la distancia por defecto.
        var kmPachuca = CalculadoraImpacto.DistanciaKm(20.0539, -99.3412, 20.1011, -98.7591, Opciones.DistanciaPorDefectoKm);
        var kmDefecto = Opciones.DistanciaPorDefectoKm;
        var ahorroEsperado =
            CalculadoraImpacto.AhorroPasajero(kmDefecto, 30m, Opciones)
            + CalculadoraImpacto.AhorroPasajero(kmPachuca, 30m, Opciones)
            + 30m; // aporte que recibió como conductor
        var co2Esperado =
            CalculadoraImpacto.Co2EvitadoKg(1, kmDefecto, Opciones) * 2
            + CalculadoraImpacto.Co2EvitadoKg(1, kmPachuca, Opciones);

        Assert.Equal(Math.Round(ahorroEsperado, 2), estadisticas.AhorroCombustible);
        Assert.Equal(Math.Round(co2Esperado, 1), estadisticas.Co2EvitadoKg);
    }
}

public sealed class CalculadoraImpactoTests
{
    private static readonly EstadisticasOptions Opciones = new();

    [Fact]
    public void Sin_coordenadas_de_destino_usa_la_distancia_por_defecto()
    {
        Assert.Equal(12, CalculadoraImpacto.DistanciaKm(20.05, -99.34, null, null, 12));
        Assert.Equal(12, CalculadoraImpacto.DistanciaKm(0, 0, 20.1, -98.7, 12));
    }

    [Fact]
    public void Un_destino_encima_del_origen_usa_la_distancia_por_defecto()
    {
        Assert.Equal(12, CalculadoraImpacto.DistanciaKm(20.05, -99.34, 20.0501, -99.3401, 12));
    }

    [Fact]
    public void Calcula_la_distancia_con_Haversine()
    {
        // UTTT (Tula) → Pachuca: unos 61 km en línea recta.
        var km = CalculadoraImpacto.DistanciaKm(20.0539, -99.3412, 20.1011, -98.7591, 12);
        Assert.InRange(km, 60, 62);
    }

    [Fact]
    public void El_ahorro_del_pasajero_compara_contra_la_tarifa_de_referencia()
    {
        // 35 + 9 × 10 = 125 de tarifa; pagó 40 → ahorró 85.
        Assert.Equal(125m, CalculadoraImpacto.TarifaReferencia(10, Opciones));
        Assert.Equal(85m, CalculadoraImpacto.AhorroPasajero(10, 40m, Opciones));
    }

    [Fact]
    public void El_ahorro_nunca_es_negativo()
    {
        Assert.Equal(0m, CalculadoraImpacto.AhorroPasajero(1, 500m, Opciones));
    }

    [Fact]
    public void El_co2_evitado_es_asientos_por_distancia_por_factor()
    {
        // 2 asientos × 10 km × 0.192 kg/km = 3.84 kg
        Assert.Equal(3.84m, CalculadoraImpacto.Co2EvitadoKg(2, 10, Opciones));
    }
}
