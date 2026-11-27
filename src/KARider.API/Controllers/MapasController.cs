using KARider.API.Common;
using KARider.API.Extensions;
using KARider.Application.Features.Viajes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KARider.API.Controllers;

/// <summary>HU-22: búsqueda de lugares para fijar el destino exacto de un viaje.</summary>
[Route("api/v1/mapas")]
[Authorize(Policy = AuthorizationPolicies.Conductor)]
public sealed class MapasController(RutaViajeService rutas) : ApiControllerBase
{
    /// <summary>
    /// Lugares que coinciden con el texto (mínimo 3 letras), del más cercano al campus al más lejano.
    /// El conductor elige uno y lo envía como <c>destinoLatitud</c>/<c>destinoLongitud</c> al publicar o editar.
    /// </summary>
    [HttpGet("lugares")]
    [ProducesResponseType<IReadOnlyList<LugarDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Lugares([FromQuery] string? texto, CancellationToken ct) =>
        Ok(await rutas.BuscarLugaresAsync(texto, ct));
}
