using KARider.API.Common;
using KARider.API.Extensions;
using KARider.Application.Common;
using KARider.Application.Features.Reservas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KARider.API.Controllers;

/// <summary>Solicitud de asientos y gestión de solicitudes por parte del conductor.</summary>
[Route("api/v1/reservas")]
public sealed class ReservasController(ReservaService reservas) : ApiControllerBase
{
    /// <summary>Aparta 1 o 2 asientos; la reserva queda confirmada de inmediato y se genera su folio.</summary>
    [HttpPost("/api/v1/viajes/{viajeId:guid}/reservas")]
    [ProducesResponseType<ReservaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Crear(Guid viajeId, CrearReservaRequest request, CancellationToken ct) =>
        CreatedFromResult(await reservas.CrearAsync(UsuarioId, viajeId, request, ct), r => $"/api/v1/reservas/{r.Id}");

    [HttpGet]
    [ProducesResponseType<PagedResult<ReservaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MisReservas([FromQuery] MisReservasQuery query, CancellationToken ct) =>
        Ok(await reservas.ListarMiasAsync(UsuarioId, query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReservaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct) =>
        FromResult(await reservas.ObtenerAsync(UsuarioId, id, ct));

    [HttpPost("{id:guid}/aceptar")]
    [Authorize(Policy = AuthorizationPolicies.Conductor)]
    [ProducesResponseType<ReservaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Aceptar(Guid id, CancellationToken ct) =>
        FromResult(await reservas.AceptarAsync(UsuarioId, id, ct));

    [HttpPost("{id:guid}/rechazar")]
    [Authorize(Policy = AuthorizationPolicies.Conductor)]
    [ProducesResponseType<ReservaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Rechazar(Guid id, CancellationToken ct) =>
        FromResult(await reservas.RechazarAsync(UsuarioId, id, ct));

    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType<ReservaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct) =>
        FromResult(await reservas.CancelarAsync(UsuarioId, id, ct));

    /// <summary>El conductor registra que recibió el aporte de combustible.</summary>
    [HttpPost("{id:guid}/aporte-pagado")]
    [Authorize(Policy = AuthorizationPolicies.Conductor)]
    [ProducesResponseType<ReservaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AportePagado(Guid id, CancellationToken ct) =>
        FromResult(await reservas.MarcarAportePagadoAsync(UsuarioId, id, ct));
}
