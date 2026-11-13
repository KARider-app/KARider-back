using KARider.API.Common;
using KARider.Application.Common;
using KARider.Application.Features.Calificaciones;
using KARider.Application.Features.Perfil;
using Microsoft.AspNetCore.Mvc;

namespace KARider.API.Controllers;

/// <summary>Perfil del usuario autenticado: datos, estadísticas, historial, reputación y credenciales (P13–P15).</summary>
[Route("api/v1/perfil")]
public sealed class PerfilController(PerfilService perfil, CalificacionService calificaciones) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PerfilDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Obtener(CancellationToken ct) => FromResult(await perfil.ObtenerAsync(UsuarioId, ct));

    [HttpPut]
    [ProducesResponseType<PerfilDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Actualizar(ActualizarPerfilRequest request, CancellationToken ct) =>
        FromResult(await perfil.ActualizarAsync(UsuarioId, request, ct));

    [HttpGet("historial")]
    [ProducesResponseType<PagedResult<HistorialItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Historial([FromQuery] HistorialQuery query, CancellationToken ct) =>
        Ok(await perfil.ObtenerHistorialAsync(UsuarioId, query, ct));

    [HttpGet("calificaciones")]
    [ProducesResponseType<ReputacionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reputacion([FromQuery] PaginacionQuery query, CancellationToken ct) =>
        FromResult(await calificaciones.ObtenerReputacionAsync(UsuarioId, query, ct));

    [HttpGet("credenciales")]
    [ProducesResponseType<CredencialesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Credenciales(CancellationToken ct) =>
        FromResult(await perfil.ObtenerCredencialesAsync(UsuarioId, ct));
}

/// <summary>Información pública de otros miembros verificados de la comunidad.</summary>
[Route("api/v1/usuarios")]
public sealed class UsuariosController(PerfilService perfil, CalificacionService calificaciones) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PerfilPublicoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PerfilPublico(Guid id, CancellationToken ct) =>
        FromResult(await perfil.ObtenerPublicoAsync(id, ct));

    /// <summary>HU-12: credenciales UTTT; solo el propio usuario puede consultarlas (403 si el id es de otro).</summary>
    [HttpGet("{id:guid}/credenciales")]
    [ProducesResponseType<CredencialesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Credenciales(Guid id, CancellationToken ct) =>
        FromResult(await perfil.ObtenerCredencialesAsync(UsuarioId, id, ct));

    [HttpGet("{id:guid}/calificaciones")]
    [ProducesResponseType<ReputacionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reputacion(Guid id, [FromQuery] PaginacionQuery query, CancellationToken ct) =>
        FromResult(await calificaciones.ObtenerReputacionAsync(id, query, ct));
}

/// <summary>Calificación mutua al concluir un viaje.</summary>
[Route("api/v1/calificaciones")]
public sealed class CalificacionesController(CalificacionService calificaciones) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<CalificacionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Calificar(CalificarRequest request, CancellationToken ct) =>
        CreatedFromResult(await calificaciones.CalificarAsync(UsuarioId, request, ct), c => $"/api/v1/usuarios/{request.EvaluadoId}/calificaciones");

    /// <summary>Compañeros de viajes completados que el usuario aún no ha calificado.</summary>
    [HttpGet("pendientes")]
    [ProducesResponseType<IReadOnlyList<CalificacionPendienteDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Pendientes(CancellationToken ct) =>
        Ok(await calificaciones.ObtenerPendientesAsync(UsuarioId, ct));
}
