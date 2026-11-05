using KARider.Application.Abstractions;
using KARider.Application.Common;
using KARider.Application.Features.Catalogos;
using KARider.Application.Features.Notificaciones;
using KARider.Domain.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KARider.Application.Features.Viajes;

/// <summary>
/// Publicación, búsqueda, detalle, edición y seguimiento de viajes (P5–P12).
/// </summary>
public sealed class ViajeService(
    IApplicationDbContext db,
    Notificador notificador,
    IOptions<InstitucionOptions> institucion,
    TimeProvider time)
{
    public async Task<PagedResult<ViajeResumenDto>> BuscarAsync(BuscarViajesQuery query, CancellationToken ct)
    {
        var ahora = time.GetUtcNow();
        var viajes = db.Viajes.AsNoTracking()
            .Where(v => v.Estado == EstadoViaje.Programado && v.FechaSalida > ahora && v.AsientosDisponibles > 0);

        if (!string.IsNullOrWhiteSpace(query.Destino))
        {
            var termino = query.Destino.Trim().ToLower();
            viajes = viajes.Where(v =>
                v.Destino.ToLower().Contains(termino) ||
                v.Paradas.Any(p => p.Nombre.ToLower().Contains(termino)));
        }

        if (query.PuntoEncuentroId is { } puntoId)
        {
            viajes = viajes.Where(v => v.PuntoEncuentroId == puntoId);
        }

        if (query.Fecha is { } fecha)
        {
            var (inicio, fin) = RangoDelDia(fecha);
            viajes = viajes.Where(v => v.FechaSalida >= inicio && v.FechaSalida < fin);
        }

        viajes = query.Orden switch
        {
            OrdenViajes.MenorAporte => viajes.OrderBy(v => v.AportePorAsiento).ThenBy(v => v.FechaSalida),
            OrdenViajes.Calificacion => viajes.OrderByDescending(v => v.Conductor!.CalificacionPromedio).ThenBy(v => v.FechaSalida),
            _ => viajes.OrderBy(v => v.FechaSalida)
        };

        return await viajes.Select(ViajeProyecciones.Resumen).ToPagedAsync(query, ct);
    }

    public async Task<PagedResult<ViajeResumenDto>> ListarPublicadosAsync(Guid conductorId, MisViajesQuery query, CancellationToken ct)
    {
        var viajes = db.Viajes.AsNoTracking().Where(v => v.ConductorId == conductorId);
        if (query.Estado is { } estado)
        {
            viajes = viajes.Where(v => v.Estado == estado);
        }

        return await viajes
            .OrderByDescending(v => v.FechaSalida)
            .Select(ViajeProyecciones.Resumen)
            .ToPagedAsync(query, ct);
    }

    public async Task<Result<ViajeDetalleDto>> ObtenerDetalleAsync(Guid viajeId, Guid usuarioId, CancellationToken ct)
    {
        var viaje = await db.Viajes.AsNoTracking()
            .Include(v => v.Conductor).ThenInclude(c => c!.Carrera)
            .Include(v => v.Vehiculo)
            .Include(v => v.PuntoEncuentro)
            .Include(v => v.Paradas)
            .Include(v => v.Reservas).ThenInclude(r => r.Pasajero).ThenInclude(p => p!.Carrera)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct);

        if (viaje is null)
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        var esPropietario = viaje.ConductorId == usuarioId;

        // Los borradores solo los ve su autor.
        if (viaje.Estado == EstadoViaje.Borrador && !esPropietario)
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        var miReserva = viaje.Reservas
            .Where(r => r.PasajeroId == usuarioId)
            .OrderByDescending(r => r.CreadoEn)
            .FirstOrDefault();

        var tieneReservaConfirmada = miReserva?.Estado is EstadoReserva.Confirmada or EstadoReserva.Completada;
        var conductor = viaje.Conductor!;

        // Privacidad: el teléfono del conductor solo se comparte con pasajeros confirmados.
        var conductorDto = new ConductorDetalleDto(
            conductor.Id,
            conductor.NombreCompleto,
            conductor.Carrera?.Nombre ?? string.Empty,
            conductor.FotoUrl,
            conductor.CalificacionPromedio,
            conductor.TotalCalificaciones,
            conductor.CorreoVerificado,
            esPropietario || tieneReservaConfirmada ? conductor.Telefono : null);

        IReadOnlyList<SolicitudReservaDto>? solicitudes = esPropietario
            ? viaje.Reservas.OrderBy(r => r.CreadoEn).Select(ToSolicitud).ToList()
            : null;

        return new ViajeDetalleDto(
            viaje.Id,
            conductorDto,
            new VehiculoDetalleDto(viaje.Vehiculo!.Modelo, viaje.Vehiculo.Color, viaje.Vehiculo.Anio, viaje.Vehiculo.Placas, viaje.Vehiculo.Verificado),
            ToDto(viaje.PuntoEncuentro!),
            viaje.Destino,
            viaje.DestinoLatitud,
            viaje.DestinoLongitud,
            viaje.Paradas.OrderBy(p => p.Orden).Select(p => new ParadaDto(p.Orden, p.Nombre)).ToList(),
            viaje.FechaSalida,
            viaje.GastoTotal,
            viaje.AportePorAsiento,
            viaje.AsientosDisponibles,
            viaje.AsientosOfrecidos,
            viaje.Notas,
            viaje.Estado,
            esPropietario,
            miReserva is null ? null : new MiReservaDto(miReserva.Id, miReserva.Folio, miReserva.Estado, miReserva.Asientos, miReserva.MontoAporte),
            solicitudes);
    }

    public async Task<Result<ViajeDetalleDto>> PublicarAsync(Guid conductorId, PublicarViajeRequest request, CancellationToken ct)
    {
        var conductor = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == conductorId && u.Activo, ct);
        if (conductor is null)
        {
            return DomainErrors.Usuario.NoEncontrado;
        }

        if (!conductor.EsConductor)
        {
            return DomainErrors.Auth.RolNoAutorizado;
        }

        var vehiculos = db.Vehiculos.Where(v => v.ConductorId == conductorId && v.Activo);
        var vehiculo = request.VehiculoId is { } vehiculoId
            ? await vehiculos.FirstOrDefaultAsync(v => v.Id == vehiculoId, ct)
            : await vehiculos.OrderBy(v => v.CreadoEn).FirstOrDefaultAsync(ct);

        if (vehiculo is null)
        {
            return request.VehiculoId is null ? DomainErrors.Vehiculo.SinVehiculo : DomainErrors.Vehiculo.NoEncontrado;
        }

        if (!await PuntoEncuentroValidoAsync(request.PuntoEncuentroId, ct))
        {
            return DomainErrors.Viaje.PuntoEncuentroInvalido;
        }

        var resultado = Viaje.Crear(
            conductorId,
            vehiculo,
            request.PuntoEncuentroId,
            request.Destino,
            request.DestinoLatitud,
            request.DestinoLongitud,
            request.FechaSalida,
            request.Asientos,
            request.GastoTotal,
            request.Notas,
            request.Paradas ?? [],
            request.Publicar,
            time.GetUtcNow());

        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        db.Viajes.Add(resultado.Value);
        await db.SaveChangesAsync(ct);

        return await ObtenerDetalleAsync(resultado.Value.Id, conductorId, ct);
    }

    public async Task<Result<ViajeDetalleDto>> EditarAsync(Guid conductorId, Guid viajeId, EditarViajeRequest request, CancellationToken ct)
    {
        var viaje = await db.Viajes
            .Include(v => v.Vehiculo)
            .Include(v => v.Paradas)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct);

        if (viaje is null)
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        if (viaje.ConductorId != conductorId)
        {
            return DomainErrors.Viaje.NoEsPropietario;
        }

        if (!await PuntoEncuentroValidoAsync(request.PuntoEncuentroId, ct))
        {
            return DomainErrors.Viaje.PuntoEncuentroInvalido;
        }

        var fechaSalidaAnterior = viaje.FechaSalida;
        var puntoEncuentroAnteriorId = viaje.PuntoEncuentroId;
        var cuposLibresAnteriores = viaje.AsientosDisponibles;

        var resultado = viaje.Actualizar(
            request.PuntoEncuentroId,
            request.Destino,
            request.DestinoLatitud,
            request.DestinoLongitud,
            request.FechaSalida,
            request.Asientos,
            request.GastoTotal,
            request.Notas,
            request.Paradas ?? [],
            viaje.Vehiculo!.Capacidad,
            time.GetUtcNow());
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        db.HistorialEdicionesViaje.Add(HistorialEdicionViaje.Registrar(
            viaje.Id, fechaSalidaAnterior, puntoEncuentroAnteriorId, cuposLibresAnteriores, viaje, request.MensajeAviso));

        var mensaje = string.IsNullOrWhiteSpace(request.MensajeAviso)
            ? $"El conductor actualizó el viaje a {viaje.Destino}: salida {FormatearHora(viaje.FechaSalida)}. Revisa el punto de encuentro."
            : request.MensajeAviso.Trim();

        // Consulta aparte (no Include) para no combinar dos colecciones en el mismo query y multiplicar filas.
        var reservasActivas = await db.Reservas
            .Where(r => r.ViajeId == viaje.Id)
            .ToListAsync(ct);

        foreach (var reserva in reservasActivas.Where(r => r.EstaActiva))
        {
            notificador.Agregar(reserva.PasajeroId, TipoNotificacion.ViajeActualizado, $"Cambios en tu viaje a {viaje.Destino}", mensaje, viaje.Id, reserva.Id);
        }

        await db.SaveChangesAsync(ct);
        await notificador.DespacharAsync(ct);

        return await ObtenerDetalleAsync(viaje.Id, conductorId, ct);
    }

    public async Task<Result<ViajeDetalleDto>> CambiarEstadoAsync(Guid conductorId, Guid viajeId, CambiarEstadoViajeRequest request, CancellationToken ct)
    {
        var viaje = await db.Viajes
            .Include(v => v.Reservas)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct);

        if (viaje is null)
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        if (viaje.ConductorId != conductorId)
        {
            return DomainErrors.Viaje.NoEsPropietario;
        }

        // Reservas activas antes del cambio: son las que deben enterarse.
        var afectadas = viaje.Reservas.Where(r => r.EstaActiva).ToList();
        var resultado = viaje.CambiarEstado(request.Estado, time.GetUtcNow());
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        var extra = string.IsNullOrWhiteSpace(request.Mensaje) ? string.Empty : $" Mensaje del conductor: {request.Mensaje.Trim()}";
        foreach (var reserva in afectadas)
        {
            var (tipo, titulo, mensaje) = (request.Estado, reserva.Estado) switch
            {
                (EstadoViaje.EnCurso, EstadoReserva.Confirmada) => (TipoNotificacion.ViajeIniciado, "Tu viaje inició", $"El conductor va en camino hacia el punto de encuentro del viaje a {viaje.Destino}."),
                (EstadoViaje.Completado, EstadoReserva.Completada) => (TipoNotificacion.ViajeCompletado, "Viaje concluido", $"Tu viaje a {viaje.Destino} concluyó. ¡Califica a tu conductor!"),
                (EstadoViaje.Cancelado, _) => (TipoNotificacion.ViajeCancelado, "Viaje cancelado", $"El conductor canceló el viaje a {viaje.Destino}. Busca otro viaje disponible."),
                (_, EstadoReserva.Rechazada) => (TipoNotificacion.ReservaRechazada, "Solicitud no atendida", $"El viaje a {viaje.Destino} salió sin confirmar tu solicitud."),
                _ => (TipoNotificacion.ViajeActualizado, "Actualización de viaje", $"El viaje a {viaje.Destino} cambió a «{request.Estado}».")
            };

            notificador.Agregar(reserva.PasajeroId, tipo, titulo, mensaje + extra, viaje.Id, reserva.Id);
        }

        await db.SaveChangesAsync(ct);
        await notificador.DespacharAsync(ct);

        return await ObtenerDetalleAsync(viaje.Id, conductorId, ct);
    }

    public async Task<Result> ActualizarUbicacionAsync(Guid conductorId, Guid viajeId, UbicacionRequest request, CancellationToken ct)
    {
        var viaje = await db.Viajes.FirstOrDefaultAsync(v => v.Id == viajeId, ct);
        if (viaje is null)
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        if (viaje.ConductorId != conductorId)
        {
            return DomainErrors.Viaje.NoEsPropietario;
        }

        var resultado = viaje.ActualizarUbicacion(request.Latitud, request.Longitud, time.GetUtcNow());
        if (resultado.IsFailure)
        {
            return resultado;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<UbicacionDto>> ObtenerUbicacionAsync(Guid usuarioId, Guid viajeId, CancellationToken ct)
    {
        var viaje = await db.Viajes.AsNoTracking()
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

        if (viaje.UbicacionLatitud is not { } lat || viaje.UbicacionLongitud is not { } lng || viaje.UbicacionActualizadaEn is not { } fecha)
        {
            return DomainErrors.Viaje.UbicacionNoDisponible;
        }

        return new UbicacionDto(lat, lng, fecha, viaje.Estado, ToDto(viaje.PuntoEncuentro!));
    }

    public async Task<Result<ResumenAporteDto>> ObtenerResumenAporteAsync(Guid usuarioId, Guid viajeId, CancellationToken ct)
    {
        var viaje = await db.Viajes.AsNoTracking()
            .Include(v => v.Reservas)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct);

        if (viaje is null)
        {
            return DomainErrors.Viaje.NoEncontrado;
        }

        if (!await EsParticipanteConfirmadoAsync(viaje, usuarioId, ct))
        {
            return DomainErrors.Viaje.AccesoDenegado;
        }

        var confirmadas = viaje.Reservas
            .Where(r => r.Estado is EstadoReserva.Confirmada or EstadoReserva.Completada)
            .ToList();
        var asientosConfirmados = confirmadas.Sum(r => r.Asientos);

        return new ResumenAporteDto(
            viaje.GastoTotal,
            viaje.AportePorAsiento,
            viaje.AsientosOfrecidos,
            asientosConfirmados,
            confirmadas.Count,
            confirmadas.Sum(r => r.MontoAporte),
            confirmadas.Count(r => r.AportePagado),
            asientosConfirmados == 0 ? viaje.AportePorAsiento : Viaje.CalcularAporte(viaje.GastoTotal, asientosConfirmados));
    }

    private async Task<bool> EsParticipanteConfirmadoAsync(Viaje viaje, Guid usuarioId, CancellationToken ct) =>
        viaje.ConductorId == usuarioId ||
        await db.Reservas.AnyAsync(
            r => r.ViajeId == viaje.Id &&
                 r.PasajeroId == usuarioId &&
                 (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.Completada),
            ct);

    private Task<bool> PuntoEncuentroValidoAsync(int puntoId, CancellationToken ct) =>
        db.PuntosEncuentro.AnyAsync(p => p.Id == puntoId && p.Activo, ct);

    private (DateTimeOffset Inicio, DateTimeOffset Fin) RangoDelDia(DateOnly fecha)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(institucion.Value.ZonaHoraria);
        var inicioLocal = fecha.ToDateTime(TimeOnly.MinValue);
        var inicio = new DateTimeOffset(inicioLocal, zona.GetUtcOffset(inicioLocal)).ToUniversalTime();
        return (inicio, inicio.AddDays(1));
    }

    private string FormatearHora(DateTimeOffset fecha)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(institucion.Value.ZonaHoraria);
        return TimeZoneInfo.ConvertTime(fecha, zona).ToString("dd/MM HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static PuntoEncuentroDto ToDto(PuntoEncuentro p) => new(p.Id, p.Nombre, p.Descripcion, p.Latitud, p.Longitud);

    private static SolicitudReservaDto ToSolicitud(Reserva r)
    {
        var pasajero = r.Pasajero!;
        var compartirTelefono = r.Estado is EstadoReserva.Confirmada or EstadoReserva.Completada;
        return new SolicitudReservaDto(
            r.Id,
            r.Folio,
            new PasajeroResumenDto(
                pasajero.Id,
                pasajero.NombreCompleto,
                pasajero.Carrera?.Nombre ?? string.Empty,
                pasajero.FotoUrl,
                pasajero.CalificacionPromedio,
                compartirTelefono ? pasajero.Telefono : null),
            r.Asientos,
            r.MontoAporte,
            r.Estado,
            r.AportePagado,
            r.CreadoEn);
    }
}
