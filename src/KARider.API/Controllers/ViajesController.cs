using KARider.API.Common;
using KARider.API.Extensions;
using KARider.Application.Common;
using KARider.Application.Features.Viajes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KARider.API.Controllers;

/// <summary>Consulta, publicación, edición y seguimiento de viajes (P5–P12).</summary>
[Route("api/v1/viajes")]
public sealed class ViajesController(ViajeService viajes) : ApiControllerBase
{
    /// <summary>Busca viajes disponibles por destino/parada, punto de encuentro y fecha.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ViajeResumenDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Buscar([FromQuery] BuscarViajesQuery query, CancellationToken ct) =>
        Ok(await viajes.BuscarAsync(query, ct));

    /// <summary>Viajes publicados por el conductor autenticado (incluye borradores).</summary>
    [HttpGet("publicados")]
    [Authorize(Policy = AuthorizationPolicies.Conductor)]
    [ProducesResponseType<PagedResult<ViajeResumenDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Publicados([FromQuery] MisViajesQuery query, CancellationToken ct) =>
        Ok(await viajes.ListarPublicadosAsync(UsuarioId, query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ViajeDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detalle(Guid id, CancellationToken ct) =>
        FromResult(await viajes.ObtenerDetalleAsync(id, UsuarioId, ct));

    /// <summary>Publica un viaje (o lo guarda como borrador con publicar=false). Calcula el aporte por asiento.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Conductor)]
    [ProducesResponseType<ViajeDetalleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar(PublicarViajeRequest request, CancellationToken ct) =>
        CreatedFromResult(await viajes.PublicarAsync(UsuarioId, request, ct), v => $"/api/v1/viajes/{v.Id}");

    /// <summary>Edita punto de encuentro, hora y cupos; notifica a los pasajeros con reserva.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Conductor)]
    [ProducesResponseType<ViajeDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Editar(Guid id, EditarViajeRequest request, CancellationToken ct) =>
        FromResult(await viajes.EditarAsync(UsuarioId, id, request, ct));

    /// <summary>Cambia el estado: Programado (publicar borrador), EnCurso, Completado o Cancelado.</summary>
    [HttpPatch("{id:guid}/estado")]
    [Authorize(Policy = AuthorizationPolicies.Conductor)]
    [ProducesResponseType<ViajeDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CambiarEstado(Guid id, CambiarEstadoViajeRequest request, CancellationToken ct) =>
        FromResult(await viajes.CambiarEstadoAsync(UsuarioId, id, request, ct));

    /// <summary>El conductor comparte su ubicación actual (geolocalización del dispositivo).</summary>
    [HttpPut("{id:guid}/ubicacion")]
    [Authorize(Policy = AuthorizationPolicies.Conductor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ActualizarUbicacion(Guid id, UbicacionRequest request, CancellationToken ct) =>
        FromResult(await viajes.ActualizarUbicacionAsync(UsuarioId, id, request, ct));

    /// <summary>Ubicación del conductor para pasajeros confirmados (P8).</summary>
    [HttpGet("{id:guid}/ubicacion")]
    [ProducesResponseType<UbicacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerUbicacion(Guid id, CancellationToken ct) =>
        FromResult(await viajes.ObtenerUbicacionAsync(UsuarioId, id, ct));

    /// <summary>HU-08: ruta real por calles (punto de encuentro → paradas → destino) para el mapa.</summary>
    [HttpGet("{id:guid}/ruta")]
    [ProducesResponseType<RutaViajeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ruta(Guid id, [FromServices] RutaViajeService rutas, CancellationToken ct) =>
        FromResult(await rutas.ObtenerRutaAsync(UsuarioId, id, ct));

    /// <summary>HU-08: tiempo estimado de llegada y distancia restante del conductor (solo participantes confirmados).</summary>
    [HttpGet("{id:guid}/llegada")]
    [ProducesResponseType<LlegadaConductorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Llegada(Guid id, [FromServices] RutaViajeService rutas, CancellationToken ct) =>
        FromResult(await rutas.ObtenerLlegadaAsync(UsuarioId, id, ct));

    /// <summary>Resumen del aporte de combustible entre los pasajeros confirmados.</summary>
    [HttpGet("{id:guid}/aportes")]
    [ProducesResponseType<ResumenAporteDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Aportes(Guid id, CancellationToken ct) =>
        FromResult(await viajes.ObtenerResumenAporteAsync(UsuarioId, id, ct));
}
