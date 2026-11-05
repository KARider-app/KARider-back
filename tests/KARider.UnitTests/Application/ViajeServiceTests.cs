using KARider.Application.Abstractions;
using KARider.Application.Common;
using KARider.Application.Features.Notificaciones;
using KARider.Application.Features.Viajes;
using KARider.Domain.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KARider.UnitTests.Application;

/// <summary>
/// Cada fase usa su propio AppDbContext (como ocurriría entre peticiones HTTP reales): evita que el
/// proveedor InMemory confunda el seguimiento de cambios al reutilizar un mismo contexto ya rastreado.
/// </summary>
public sealed class ViajeServiceTests
{
    private readonly string _baseDatos = Guid.NewGuid().ToString();
    private Guid _conductorId;
    private Guid _pasajeroId;

    private AppDbContext NuevoContexto() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(_baseDatos)
        .Options);

    private async Task<Guid> SembrarViajeAsync(int asientos = 3, int? asientosReservados = null, string[]? paradas = null)
    {
        await using var db = NuevoContexto();
        db.Carreras.Add(new Carrera { Id = 1, Nombre = "ISC" });
        db.PuntosEncuentro.AddRange(
            new PuntoEncuentro { Id = 1, Nombre = "Entrada UTTT", Descripcion = "Caseta principal", Activo = true },
            new PuntoEncuentro { Id = 2, Nombre = "Plaza Juárez", Descripcion = "Centro de Tula", Activo = true });

        // ObtenerDetalleAsync incluye Conductor.Carrera (relación requerida): sin un Usuario real,
        // el proveedor InMemory descarta la fila del viaje por completo al hacer el Include.
        var conductor = Usuario.Crear("Conductor de prueba", "22300001", "7731234567", "conductor@uttt.edu.mx", "hash", Rol.Conductor, 1, 1);
        db.Usuarios.Add(conductor);
        _conductorId = conductor.Id;

        var vehiculo = Vehiculo.Crear(_conductorId, "Nissan Versa", "Azul", 2022, "HNX-421-C", 4);
        db.Vehiculos.Add(vehiculo);

        var viaje = Viaje.Crear(
            _conductorId, vehiculo, 1, "Pachuca", null, null,
            DateTimeOffset.UtcNow.AddHours(2), asientos, 160m, "Nota original", paradas ?? [],
            publicar: true, DateTimeOffset.UtcNow).Value;
        db.Viajes.Add(viaje);

        if (asientosReservados is { } reservados)
        {
            var pasajero = Usuario.Crear("Pasajero de prueba", "22300002", "7731234568", "pasajero@uttt.edu.mx", "hash", Rol.Pasajero, 1, 1);
            db.Usuarios.Add(pasajero);
            _pasajeroId = pasajero.Id;
            var reserva = Reserva.Crear(viaje, pasajero.Id, reservados, DateTimeOffset.UtcNow).Value;
            db.Reservas.Add(reserva);
        }

        await db.SaveChangesAsync();
        return viaje.Id;
    }

    private async Task<Result<ViajeDetalleDto>> EditarAsync(Guid viajeId, EditarViajeRequest request, Guid? conductorId = null)
    {
        await using var db = NuevoContexto();
        var notificador = new Notificador(db, new PushFalso(), NullLogger<Notificador>.Instance);
        var viajes = new ViajeService(db, notificador, Options.Create(new InstitucionOptions()), TimeProvider.System);
        return await viajes.EditarAsync(conductorId ?? _conductorId, viajeId, request, default);
    }

    private static EditarViajeRequest Request(
        int puntoEncuentroId = 2,
        string destino = "Centro de Tula",
        int asientos = 3,
        decimal gastoTotal = 200m,
        string? notas = "Nota actualizada",
        string[]? paradas = null,
        string? mensajeAviso = null) =>
        new(puntoEncuentroId, destino, null, null, DateTimeOffset.UtcNow.AddHours(3), asientos, gastoTotal, notas,
            paradas ?? [], mensajeAviso);

    [Fact]
    public async Task Editar_actualiza_todos_los_campos_y_notifica_al_pasajero_con_reserva()
    {
        var viajeId = await SembrarViajeAsync(asientos: 3, asientosReservados: 2);

        var resultado = await EditarAsync(viajeId, Request(asientos: 3));

        Assert.True(resultado.IsSuccess, resultado.IsFailure ? resultado.Error.Code : "");
        await using var db = NuevoContexto();
        var actualizado = await db.Viajes.FirstAsync(v => v.Id == viajeId);
        Assert.Equal(2, actualizado.PuntoEncuentroId);
        Assert.Equal("Centro de Tula", actualizado.Destino);
        Assert.Equal(200m, actualizado.GastoTotal);
        Assert.Equal("Nota actualizada", actualizado.Notas);
        Assert.Equal(3, actualizado.AsientosOfrecidos);
        Assert.Equal(1, actualizado.AsientosDisponibles);
        Assert.Equal(1, actualizado.NumeroEdiciones);

        Assert.Contains(db.Notificaciones, n => n.UsuarioId == _pasajeroId && n.Tipo == TipoNotificacion.ViajeActualizado);
    }

    [Fact]
    public async Task Editar_con_menos_asientos_que_los_reservados_falla_y_no_guarda_cambios()
    {
        var viajeId = await SembrarViajeAsync(asientos: 3, asientosReservados: 2);

        var resultado = await EditarAsync(viajeId, Request(asientos: 1));

        Assert.Equal("VIAJE_ASIENTOS_INSUFICIENTES", resultado.Error.Code);
        await using var db = NuevoContexto();
        Assert.Equal("Pachuca", (await db.Viajes.FirstAsync(v => v.Id == viajeId)).Destino);
    }

    [Fact]
    public async Task Editar_un_viaje_ajeno_devuelve_prohibido()
    {
        var viajeId = await SembrarViajeAsync();

        var resultado = await EditarAsync(viajeId, Request(), conductorId: Guid.NewGuid());

        Assert.Equal("VIAJE_NO_ES_PROPIETARIO", resultado.Error.Code);
    }

    private sealed class PushFalso : IPushSender
    {
        public string? PublicKey => null;

        public Task<IReadOnlyCollection<string>> SendAsync(
            IReadOnlyCollection<SuscripcionPush> suscripciones, PushMessage message, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);
    }
}
