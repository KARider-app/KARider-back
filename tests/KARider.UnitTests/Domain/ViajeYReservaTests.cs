using KARider.Domain.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;

namespace KARider.UnitTests.Domain;

public sealed class ViajeYReservaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);

    private static Vehiculo CrearVehiculo(Guid conductorId, int capacidad = 4) =>
        Vehiculo.Crear(conductorId, "Nissan Versa", "Azul", 2022, "hnx-421-c", capacidad);

    private static Viaje CrearViaje(Guid conductorId, int asientos = 3, decimal gasto = 160m)
    {
        var resultado = Viaje.Crear(
            conductorId, CrearVehiculo(conductorId), 1, "Pachuca", null, null,
            Ahora.AddHours(2), asientos, gasto, null, ["Actopan Centro"], publicar: true, Ahora);
        Assert.True(resultado.IsSuccess);
        return resultado.Value;
    }

    [Theory]
    [InlineData(160, 3, 40)]
    [InlineData(100, 1, 50)]
    [InlineData(100, 2, 33.33)]
    public void CalcularAporte_divide_entre_pasajeros_mas_conductor(decimal gasto, int asientos, decimal esperado) =>
        Assert.Equal(esperado, Viaje.CalcularAporte(gasto, asientos));

    [Fact]
    public void Crear_con_fecha_pasada_falla_con_codigo_especifico()
    {
        var conductor = Guid.NewGuid();
        var resultado = Viaje.Crear(conductor, CrearVehiculo(conductor), 1, "Tula", null, null,
            Ahora.AddMinutes(-1), 2, 100, null, [], true, Ahora);

        Assert.True(resultado.IsFailure);
        Assert.Equal("VIAJE_FECHA_PASADA", resultado.Error.Code);
    }

    [Fact]
    public void Crear_con_mas_asientos_que_la_capacidad_falla()
    {
        var conductor = Guid.NewGuid();
        var resultado = Viaje.Crear(conductor, CrearVehiculo(conductor, capacidad: 2), 1, "Tula", null, null,
            Ahora.AddHours(1), 3, 100, null, [], true, Ahora);

        Assert.Equal("VEHICULO_CAPACIDAD_INSUFICIENTE", resultado.Error.Code);
    }

    [Fact]
    public void Crear_normaliza_fecha_a_utc_y_placas_a_mayusculas()
    {
        var conductor = Guid.NewGuid();
        var vehiculo = CrearVehiculo(conductor);
        var salidaLocal = new DateTimeOffset(2026, 9, 20, 14, 30, 0, TimeSpan.FromHours(-6));
        var viaje = Viaje.Crear(conductor, vehiculo, 1, "Tula", null, null, salidaLocal, 2, 90, null, [], true, Ahora).Value;

        Assert.Equal(TimeSpan.Zero, viaje.FechaSalida.Offset);
        Assert.Equal("HNX-421-C", vehiculo.Placas);
    }

    [Fact]
    public void Reserva_aparta_asientos_y_cancelacion_los_libera()
    {
        var viaje = CrearViaje(Guid.NewGuid());
        var reserva = Reserva.Crear(viaje, Guid.NewGuid(), 2, Ahora).Value;

        Assert.Equal(1, viaje.AsientosDisponibles);
        Assert.Equal(80m, reserva.MontoAporte);
        Assert.StartsWith("KAR-UTTT-2026-", reserva.Folio, StringComparison.Ordinal);

        Assert.True(reserva.Cancelar(viaje, Ahora).IsSuccess);
        Assert.Equal(3, viaje.AsientosDisponibles);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
    }

    [Fact]
    public void Reserva_se_confirma_automaticamente_al_crearse()
    {
        var viaje = CrearViaje(Guid.NewGuid());
        var reserva = Reserva.Crear(viaje, Guid.NewGuid(), 1, Ahora).Value;

        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.True(reserva.EstaActiva);
        Assert.Equal(Ahora, reserva.RespondidaEn);
    }

    [Fact]
    public void Reserva_ya_confirmada_no_se_puede_rechazar()
    {
        var viaje = CrearViaje(Guid.NewGuid());
        var reserva = Reserva.Crear(viaje, Guid.NewGuid(), 1, Ahora).Value;

        Assert.Equal("RESERVA_ESTADO_INVALIDO", reserva.Rechazar(viaje, Ahora).Error.Code);
        Assert.Equal(2, viaje.AsientosDisponibles);
    }

    [Fact]
    public void Reserva_sin_cupo_suficiente_indica_asientos_restantes()
    {
        var viaje = CrearViaje(Guid.NewGuid(), asientos: 1);
        var resultado = Reserva.Crear(viaje, Guid.NewGuid(), 2, Ahora);

        Assert.Equal("VIAJE_SIN_CUPO", resultado.Error.Code);
        Assert.Contains("1 asiento", resultado.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_se_puede_reservar_un_viaje_propio()
    {
        var conductor = Guid.NewGuid();
        var resultado = Reserva.Crear(CrearViaje(conductor), conductor, 1, Ahora);

        Assert.Equal("RESERVA_VIAJE_PROPIO", resultado.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Reserva_con_asientos_fuera_de_rango_falla(int asientos)
    {
        var resultado = Reserva.Crear(CrearViaje(Guid.NewGuid()), Guid.NewGuid(), asientos, Ahora);
        Assert.Equal("RESERVA_ASIENTOS_INVALIDOS", resultado.Error.Code);
    }

    [Fact]
    public void Borrador_no_acepta_reservas()
    {
        var conductor = Guid.NewGuid();
        var borrador = Viaje.Crear(conductor, CrearVehiculo(conductor), 1, "Tula", null, null,
            Ahora.AddHours(1), 2, 100, null, [], publicar: false, Ahora).Value;

        Assert.Equal(EstadoViaje.Borrador, borrador.Estado);
        Assert.Equal("VIAJE_NO_DISPONIBLE", Reserva.Crear(borrador, Guid.NewGuid(), 1, Ahora).Error.Code);
    }

    [Fact]
    public void Transicion_invalida_de_estado_es_rechazada()
    {
        var viaje = CrearViaje(Guid.NewGuid());
        var resultado = viaje.CambiarEstado(EstadoViaje.Completado, Ahora);

        Assert.Equal("VIAJE_TRANSICION_INVALIDA", resultado.Error.Code);
    }

    private static Result ActualizarViaje(
        Viaje viaje,
        int puntoEncuentroId,
        DateTimeOffset fechaSalida,
        int asientos,
        int capacidadVehiculo,
        decimal gastoTotal = 160m) =>
        viaje.Actualizar(
            puntoEncuentroId, viaje.Destino, null, null, fechaSalida,
            asientos, gastoTotal, viaje.Notas, [], capacidadVehiculo, Ahora);

    [Fact]
    public void Editar_asientos_ofrecidos_no_puede_ser_menor_a_los_reservados()
    {
        var viaje = CrearViaje(Guid.NewGuid(), asientos: 3);
        Reserva.Crear(viaje, Guid.NewGuid(), 2, Ahora);

        var resultado = ActualizarViaje(viaje, 1, Ahora.AddHours(3), asientos: 1, capacidadVehiculo: 4);

        Assert.Equal("VIAJE_ASIENTOS_INSUFICIENTES", resultado.Error.Code);
        Assert.True(ActualizarViaje(viaje, 1, Ahora.AddHours(3), asientos: 2, capacidadVehiculo: 4).IsSuccess);
        Assert.Equal(2, viaje.AsientosOfrecidos);
        Assert.Equal(0, viaje.AsientosDisponibles);
    }

    [Fact]
    public void Editar_reemplaza_destino_gasto_notas_y_paradas()
    {
        var viaje = CrearViaje(Guid.NewGuid(), asientos: 3); // nace con la parada "Actopan Centro"
        Assert.Equal(["Actopan Centro"], viaje.Paradas.Select(p => p.Nombre));

        var resultado = viaje.Actualizar(
            2, "Centro de Tula", null, null, Ahora.AddHours(3),
            3, 200m, "Nota nueva", ["Plaza Galerías"], capacidadVehiculo: 4, Ahora);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Centro de Tula", viaje.Destino);
        Assert.Equal(200m, viaje.GastoTotal);
        Assert.Equal(50m, viaje.AportePorAsiento); // 200 / (3 + 1)
        Assert.Equal("Nota nueva", viaje.Notas);
        Assert.Equal(["Plaza Galerías"], viaje.Paradas.Select(p => p.Nombre));
    }

    [Fact]
    public void Editar_sin_paradas_deja_el_viaje_sin_paradas()
    {
        var viaje = CrearViaje(Guid.NewGuid(), asientos: 3);

        viaje.Actualizar(1, viaje.Destino, null, null, Ahora.AddHours(3), 3, 160m, null, [], capacidadVehiculo: 4, Ahora);

        Assert.Empty(viaje.Paradas);
    }

    [Fact]
    public void Editar_viaje_incrementa_numero_de_ediciones_solo_si_tiene_exito()
    {
        var viaje = CrearViaje(Guid.NewGuid(), asientos: 3);
        Assert.Equal(0, viaje.NumeroEdiciones);

        Assert.True(ActualizarViaje(viaje, 1, Ahora.AddHours(3), asientos: 2, capacidadVehiculo: 4).IsSuccess);
        Assert.True(ActualizarViaje(viaje, 1, Ahora.AddHours(4), asientos: 3, capacidadVehiculo: 4).IsSuccess);
        Assert.True(ActualizarViaje(viaje, 1, Ahora.AddMinutes(-5), asientos: 3, capacidadVehiculo: 4).IsFailure);

        Assert.Equal(2, viaje.NumeroEdiciones);
    }

    [Fact]
    public void Historial_de_edicion_conserva_valores_anteriores_y_nuevos()
    {
        var viaje = CrearViaje(Guid.NewGuid(), asientos: 3);
        var salidaAnterior = viaje.FechaSalida;

        ActualizarViaje(viaje, 2, Ahora.AddHours(5), asientos: 2, capacidadVehiculo: 4);
        var historial = HistorialEdicionViaje.Registrar(viaje.Id, salidaAnterior, 1, 3, viaje, "  Cambio de horario  ");

        Assert.Equal(salidaAnterior, historial.FechaSalidaAnterior);
        Assert.Equal(Ahora.AddHours(5), historial.FechaSalidaNueva);
        Assert.Equal(1, historial.PuntoEncuentroAnteriorId);
        Assert.Equal(2, historial.PuntoEncuentroNuevoId);
        Assert.Equal(3, historial.CuposLibresAnteriores);
        Assert.Equal(2, historial.CuposLibresNuevos);
        Assert.Equal("Cambio de horario", historial.MensajeAviso);
    }

    [Fact]
    public void Ubicacion_solo_se_comparte_en_viajes_activos()
    {
        var viaje = CrearViaje(Guid.NewGuid());
        Assert.True(viaje.ActualizarUbicacion(20.08, -99.34, Ahora).IsSuccess);

        viaje.CambiarEstado(EstadoViaje.Cancelado, Ahora);
        Assert.Null(viaje.UbicacionLatitud);
        Assert.Equal("VIAJE_UBICACION_NO_PERMITIDA", viaje.ActualizarUbicacion(20.08, -99.34, Ahora).Error.Code);
    }
}
