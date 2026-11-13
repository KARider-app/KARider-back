using FluentValidation;
using KARider.Application.Abstractions;
using KARider.Application.Common;
using KARider.Application.Features.Vehiculos;
using KARider.Domain.Common;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KARider.Application.Features.Perfil;

public enum FiltroRolHistorial
{
    Todos,
    Pasajero,
    Conductor
}

public sealed record ActualizarPerfilRequest(string Telefono, int? Cuatrimestre, string? FotoUrl);

public sealed record HistorialQuery(FiltroRolHistorial Rol = FiltroRolHistorial.Todos, int Pagina = 1, int TamanoPagina = 20) : IPaginado;

public sealed record EstadisticasDto(
    int ViajesRealizados,
    int ViajesComoConductor,
    int ViajesComoPasajero,
    decimal AhorroCombustible,
    decimal Co2EvitadoKg);

public sealed record PerfilDto(
    Guid Id,
    string NombreCompleto,
    string CorreoInstitucional,
    string Matricula,
    string Telefono,
    string Carrera,
    int? Cuatrimestre,
    string? FotoUrl,
    Rol Rol,
    bool CorreoVerificado,
    decimal Calificacion,
    int TotalCalificaciones,
    EstadisticasDto Estadisticas,
    DateTimeOffset MiembroDesde);

public sealed record PerfilPublicoDto(
    Guid Id,
    string NombreCompleto,
    string Carrera,
    int? Cuatrimestre,
    string? FotoUrl,
    Rol Rol,
    bool Verificado,
    decimal Calificacion,
    int TotalCalificaciones);

public sealed record CompaneroDto(Guid Id, string NombreCompleto, string? Vehiculo, decimal Calificacion);

public sealed record HistorialItemDto(
    Guid ViajeId,
    Rol RolEnViaje,
    DateTimeOffset FechaSalida,
    decimal Monto,
    string Origen,
    string Destino,
    EstadoViaje EstadoViaje,
    EstadoReserva? EstadoReserva,
    bool? AportePagado,
    int? Pasajeros,
    CompaneroDto? Companero,
    IReadOnlyList<string>? NombresPasajeros = null);

public sealed record CredencialesDto(
    string CorreoInstitucional,
    string Matricula,
    string Carrera,
    string Estatus,
    bool CorreoVerificado,
    IReadOnlyList<VehiculoDto> Vehiculos);

public sealed class ActualizarPerfilRequestValidator : AbstractValidator<ActualizarPerfilRequest>
{
    public ActualizarPerfilRequestValidator()
    {
        RuleFor(x => x.Telefono).Telefono();
        RuleFor(x => x.Cuatrimestre)
            .InclusiveBetween(1, 11).When(x => x.Cuatrimestre.HasValue)
            .WithMessage("El cuatrimestre debe estar entre 1 y 11.");
        RuleFor(x => x.FotoUrl)
            .MaximumLength(500).WithMessage("La URL de la foto no puede exceder 500 caracteres.")
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .When(x => !string.IsNullOrWhiteSpace(x.FotoUrl))
            .WithMessage("La foto debe ser una URL https válida.");
    }
}

public sealed class HistorialQueryValidator : AbstractValidator<HistorialQuery>
{
    public HistorialQueryValidator()
    {
        this.ReglasPaginacion();
        RuleFor(x => x.Rol).IsInEnum().WithMessage("El filtro debe ser Todos, Pasajero o Conductor.");
    }
}

/// <summary>Perfil, estadísticas, historial y credenciales UTTT (P13, P15).</summary>
public sealed class PerfilService(IApplicationDbContext db, IOptions<EstadisticasOptions> estadisticas)
{
    public async Task<Result<PerfilDto>> ObtenerAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .Include(u => u.Carrera)
            .FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo, ct);

        if (usuario is null)
        {
            return DomainErrors.Usuario.NoEncontrado;
        }

        return new PerfilDto(
            usuario.Id,
            usuario.NombreCompleto,
            usuario.CorreoInstitucional,
            usuario.Matricula,
            usuario.Telefono,
            usuario.Carrera?.Nombre ?? string.Empty,
            usuario.Cuatrimestre,
            usuario.FotoUrl,
            usuario.Rol,
            usuario.CorreoVerificado,
            usuario.CalificacionPromedio,
            usuario.TotalCalificaciones,
            await CalcularEstadisticasAsync(usuarioId, ct),
            usuario.CreadoEn);
    }

    public async Task<Result<PerfilDto>> ActualizarAsync(Guid usuarioId, ActualizarPerfilRequest request, CancellationToken ct)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo, ct);
        if (usuario is null)
        {
            return DomainErrors.Usuario.NoEncontrado;
        }

        usuario.ActualizarPerfil(request.Telefono, request.Cuatrimestre, request.FotoUrl);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(usuarioId, ct);
    }

    public async Task<Result<PerfilPublicoDto>> ObtenerPublicoAsync(Guid usuarioId, CancellationToken ct)
    {
        var perfil = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId && u.Activo)
            .Select(u => new PerfilPublicoDto(
                u.Id,
                u.NombreCompleto,
                u.Carrera!.Nombre,
                u.Cuatrimestre,
                u.FotoUrl,
                u.Rol,
                u.CorreoVerificado,
                u.CalificacionPromedio,
                u.TotalCalificaciones))
            .FirstOrDefaultAsync(ct);

        return perfil is null ? DomainErrors.Usuario.NoEncontrado : perfil;
    }

    public async Task<Result<CredencialesDto>> ObtenerCredencialesAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .Include(u => u.Carrera)
            .FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo, ct);

        if (usuario is null)
        {
            return DomainErrors.Usuario.NoEncontrado;
        }

        var vehiculos = await db.Vehiculos.AsNoTracking()
            .Where(v => v.ConductorId == usuarioId && v.Activo)
            .Select(v => new VehiculoDto(v.Id, v.Modelo, v.Color, v.Anio, v.Placas, v.Capacidad, v.Verificado))
            .ToListAsync(ct);

        return new CredencialesDto(
            usuario.CorreoInstitucional,
            usuario.Matricula,
            usuario.Carrera?.Nombre ?? string.Empty,
            usuario.CorreoVerificado ? "Alumno regular activo" : "Pendiente de verificación",
            usuario.CorreoVerificado,
            vehiculos);
    }

    /// <summary>
    /// Historial del usuario autenticado (HU-10). Regla de negocio 1: solo viajes en curso o completados;
    /// los cancelados también se listan (la PWA los etiqueta aparte). Los programados aún no son historial.
    /// </summary>
    public async Task<PagedResult<HistorialItemDto>> ObtenerHistorialAsync(Guid usuarioId, HistorialQuery query, CancellationToken ct)
    {
        // Se toman las primeras N filas de cada fuente y se combinan en memoria para paginar la unión.
        var limite = query.Pagina * query.TamanoPagina;
        var items = new List<HistorialItemDto>();
        var total = 0;

        if (query.Rol is FiltroRolHistorial.Todos or FiltroRolHistorial.Conductor)
        {
            var comoConductor = db.Viajes.AsNoTracking()
                .Where(v => v.ConductorId == usuarioId
                    && (v.Estado == EstadoViaje.EnCurso || v.Estado == EstadoViaje.Completado || v.Estado == EstadoViaje.Cancelado));
            total += await comoConductor.CountAsync(ct);
            var filas = await comoConductor
                .OrderByDescending(v => v.FechaSalida)
                .Take(limite)
                .Select(v => new
                {
                    v.Id,
                    v.FechaSalida,
                    Origen = v.PuntoEncuentro!.Nombre,
                    v.Destino,
                    v.Estado,
                    Reservas = v.Reservas
                        .Where(r => r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.Completada)
                        .Select(r => new { r.MontoAporte, r.Asientos, Nombre = r.Pasajero!.NombreCompleto })
                        .ToList(),
                })
                .ToListAsync(ct);

            items.AddRange(filas.Select(v => new HistorialItemDto(
                v.Id,
                Rol.Conductor,
                v.FechaSalida,
                v.Reservas.Sum(r => r.MontoAporte),
                v.Origen,
                v.Destino,
                v.Estado,
                null,
                null,
                v.Reservas.Sum(r => r.Asientos),
                null,
                v.Reservas.Select(r => r.Nombre).ToList())));
        }

        if (query.Rol is FiltroRolHistorial.Todos or FiltroRolHistorial.Pasajero)
        {
            // Solicitudes pendientes o rechazadas nunca fueron un viaje del pasajero; las canceladas sí se muestran.
            var comoPasajero = db.Reservas.AsNoTracking()
                .Where(r => r.PasajeroId == usuarioId
                    && (r.Estado == EstadoReserva.Cancelada
                        || ((r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.Completada)
                            && (r.Viaje!.Estado == EstadoViaje.EnCurso
                                || r.Viaje.Estado == EstadoViaje.Completado
                                || r.Viaje.Estado == EstadoViaje.Cancelado))));
            total += await comoPasajero.CountAsync(ct);
            items.AddRange(await comoPasajero
                .OrderByDescending(r => r.Viaje!.FechaSalida)
                .Take(limite)
                .Select(r => new HistorialItemDto(
                    r.ViajeId,
                    Rol.Pasajero,
                    r.Viaje!.FechaSalida,
                    r.MontoAporte,
                    r.Viaje.PuntoEncuentro!.Nombre,
                    r.Viaje.Destino,
                    r.Viaje.Estado,
                    r.Estado,
                    r.AportePagado,
                    null,
                    new CompaneroDto(
                        r.Viaje.ConductorId,
                        r.Viaje.Conductor!.NombreCompleto,
                        r.Viaje.Vehiculo!.Modelo + " (" + r.Viaje.Vehiculo.Color + ")",
                        r.Viaje.Conductor.CalificacionPromedio),
                    null))
                .ToListAsync(ct));
        }

        var pagina = items
            .OrderByDescending(i => i.FechaSalida)
            .Skip((query.Pagina - 1) * query.TamanoPagina)
            .Take(query.TamanoPagina)
            .ToList();

        return new PagedResult<HistorialItemDto>(pagina, query.Pagina, query.TamanoPagina, total);
    }

    /// <summary>
    /// Indicadores del perfil (regla de negocio 2): se calculan solo con viajes completados y con las fórmulas
    /// de <see cref="CalculadoraImpacto"/>, documentadas en docs/formulas-impacto.md.
    /// </summary>
    private async Task<EstadisticasDto> CalcularEstadisticasAsync(Guid usuarioId, CancellationToken ct)
    {
        var opciones = estadisticas.Value;

        var viajesConductor = await db.Viajes.AsNoTracking()
            .Where(v => v.ConductorId == usuarioId && v.Estado == EstadoViaje.Completado)
            .Select(v => new
            {
                OrigenLatitud = v.PuntoEncuentro!.Latitud,
                OrigenLongitud = v.PuntoEncuentro.Longitud,
                v.DestinoLatitud,
                v.DestinoLongitud,
                Aportes = v.Reservas.Where(r => r.Estado == EstadoReserva.Completada).Sum(r => (decimal?)r.MontoAporte) ?? 0,
                Asientos = v.Reservas.Where(r => r.Estado == EstadoReserva.Completada).Sum(r => (int?)r.Asientos) ?? 0,
            })
            .ToListAsync(ct);

        var reservasPasajero = await db.Reservas.AsNoTracking()
            .Where(r => r.PasajeroId == usuarioId && r.Estado == EstadoReserva.Completada)
            .Select(r => new
            {
                OrigenLatitud = r.Viaje!.PuntoEncuentro!.Latitud,
                OrigenLongitud = r.Viaje.PuntoEncuentro.Longitud,
                r.Viaje.DestinoLatitud,
                r.Viaje.DestinoLongitud,
                r.MontoAporte,
                r.Asientos,
            })
            .ToListAsync(ct);

        decimal ahorro = 0;
        decimal co2 = 0;

        // Conductor: los aportes recibidos son gasolina que ya no paga solo.
        foreach (var v in viajesConductor)
        {
            var km = CalculadoraImpacto.DistanciaKm(v.OrigenLatitud, v.OrigenLongitud, v.DestinoLatitud, v.DestinoLongitud, opciones.DistanciaPorDefectoKm);
            ahorro += v.Aportes;
            co2 += CalculadoraImpacto.Co2EvitadoKg(v.Asientos, km, opciones);
        }

        // Pasajero: tarifa de referencia de transporte privado menos el aporte que pagó.
        foreach (var r in reservasPasajero)
        {
            var km = CalculadoraImpacto.DistanciaKm(r.OrigenLatitud, r.OrigenLongitud, r.DestinoLatitud, r.DestinoLongitud, opciones.DistanciaPorDefectoKm);
            ahorro += CalculadoraImpacto.AhorroPasajero(km, r.MontoAporte, opciones);
            co2 += CalculadoraImpacto.Co2EvitadoKg(r.Asientos, km, opciones);
        }

        return new EstadisticasDto(
            viajesConductor.Count + reservasPasajero.Count,
            viajesConductor.Count,
            reservasPasajero.Count,
            Math.Round(ahorro, 2),
            Math.Round(co2, 1));
    }
}
