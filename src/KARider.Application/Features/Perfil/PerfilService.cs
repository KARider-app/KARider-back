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
    CompaneroDto? Companero);

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
    public const string EstatusVerificado = "Alumno Regular Activo";
    public const string EstatusPendiente = "Pendiente de verificación";

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

    /// <summary>
    /// HU-12, regla 3: un usuario solo lee sus propias credenciales; el id del JWT debe coincidir con el solicitado.
    /// </summary>
    public async Task<Result<CredencialesDto>> ObtenerCredencialesAsync(Guid solicitanteId, Guid usuarioId, CancellationToken ct) =>
        solicitanteId != usuarioId
            ? DomainErrors.Usuario.CredencialesAjenas
            : await ObtenerCredencialesAsync(usuarioId, ct);

    /// <summary>
    /// Ficha institucional (P15). Regla 1: el bloque de vehículo solo existe para conductores.
    /// Regla 2: el estatus se deriva siempre de <c>correo_verificado</c>.
    /// </summary>
    public async Task<Result<CredencialesDto>> ObtenerCredencialesAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .Include(u => u.Carrera)
            .FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo, ct);

        if (usuario is null)
        {
            return DomainErrors.Usuario.NoEncontrado;
        }

        IReadOnlyList<VehiculoDto> vehiculos = usuario.Rol == Rol.Conductor
            ? await db.Vehiculos.AsNoTracking()
                .Where(v => v.ConductorId == usuarioId && v.Activo)
                .Select(v => new VehiculoDto(v.Id, v.Modelo, v.Color, v.Anio, v.Placas, v.Capacidad, v.Verificado))
                .ToListAsync(ct)
            : [];

        return new CredencialesDto(
            usuario.CorreoInstitucional,
            usuario.Matricula,
            usuario.Carrera?.Nombre ?? string.Empty,
            usuario.CorreoVerificado ? EstatusVerificado : EstatusPendiente,
            usuario.CorreoVerificado,
            vehiculos);
    }

    public async Task<PagedResult<HistorialItemDto>> ObtenerHistorialAsync(Guid usuarioId, HistorialQuery query, CancellationToken ct)
    {
        // Se toman las primeras N filas de cada fuente y se combinan en memoria para paginar la unión.
        var limite = query.Pagina * query.TamanoPagina;
        var items = new List<HistorialItemDto>();
        var total = 0;

        if (query.Rol is FiltroRolHistorial.Todos or FiltroRolHistorial.Conductor)
        {
            var comoConductor = db.Viajes.AsNoTracking()
                .Where(v => v.ConductorId == usuarioId && v.Estado != EstadoViaje.Borrador);
            total += await comoConductor.CountAsync(ct);
            items.AddRange(await comoConductor
                .OrderByDescending(v => v.FechaSalida)
                .Take(limite)
                .Select(v => new HistorialItemDto(
                    v.Id,
                    Rol.Conductor,
                    v.FechaSalida,
                    v.Reservas
                        .Where(r => r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.Completada)
                        .Sum(r => r.MontoAporte),
                    v.PuntoEncuentro!.Nombre,
                    v.Destino,
                    v.Estado,
                    null,
                    null,
                    v.Reservas
                        .Where(r => r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.Completada)
                        .Sum(r => r.Asientos),
                    null))
                .ToListAsync(ct));
        }

        if (query.Rol is FiltroRolHistorial.Todos or FiltroRolHistorial.Pasajero)
        {
            var comoPasajero = db.Reservas.AsNoTracking().Where(r => r.PasajeroId == usuarioId);
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
                        r.Viaje.Conductor.CalificacionPromedio)))
                .ToListAsync(ct));
        }

        var pagina = items
            .OrderByDescending(i => i.FechaSalida)
            .Skip((query.Pagina - 1) * query.TamanoPagina)
            .Take(query.TamanoPagina)
            .ToList();

        return new PagedResult<HistorialItemDto>(pagina, query.Pagina, query.TamanoPagina, total);
    }

    private async Task<EstadisticasDto> CalcularEstadisticasAsync(Guid usuarioId, CancellationToken ct)
    {
        var comoConductor = await db.Viajes.AsNoTracking()
            .CountAsync(v => v.ConductorId == usuarioId && v.Estado == EstadoViaje.Completado, ct);

        var reservasConductor = db.Reservas.AsNoTracking()
            .Where(r => r.Estado == EstadoReserva.Completada && r.Viaje!.ConductorId == usuarioId);
        var aportesRecibidos = await reservasConductor.SumAsync(r => (decimal?)r.MontoAporte, ct) ?? 0;
        var asientosTransportados = await reservasConductor.SumAsync(r => (int?)r.Asientos, ct) ?? 0;

        var reservasPasajero = db.Reservas.AsNoTracking()
            .Where(r => r.Estado == EstadoReserva.Completada && r.PasajeroId == usuarioId);
        var comoPasajero = await reservasPasajero.CountAsync(ct);
        var asientosComoPasajero = await reservasPasajero.SumAsync(r => (int?)r.Asientos, ct) ?? 0;

        // Ahorro del pasajero: lo que costaría hacer el trayecto solo (gasto total) menos su aporte.
        var ahorroPasajero = await reservasPasajero.SumAsync(r => (decimal?)(r.Viaje!.GastoTotal - r.MontoAporte), ct) ?? 0;

        var co2 = (asientosTransportados + asientosComoPasajero) * estadisticas.Value.KgCo2PorAsientoCompartido;

        return new EstadisticasDto(
            comoConductor + comoPasajero,
            comoConductor,
            comoPasajero,
            Math.Round(aportesRecibidos + ahorroPasajero, 2),
            Math.Round(co2, 1));
    }
}
