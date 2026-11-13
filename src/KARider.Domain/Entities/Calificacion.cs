using KARider.Domain.Common;
using KARider.Domain.Enums;
using KARider.Domain.Errors;

namespace KARider.Domain.Entities;

///Calificación mutua entre conductor y pasajero al concluir un viaje 
public sealed class Calificacion : Entity
{
    private Calificacion()
    {
    }

    public Guid ViajeId { get; private set; }

    public Viaje? Viaje { get; private set; }

    public Guid EvaluadorId { get; private set; }

    public Usuario? Evaluador { get; private set; }

    public Guid EvaluadoId { get; private set; }

    public Usuario? Evaluado { get; private set; }

    /// <summary>Rol que tenía el evaluado en el viaje.</summary>
    public Rol RolEvaluado { get; private set; }

    public int Estrellas { get; private set; }

    public string? Comentario { get; private set; }

    public List<string> Etiquetas { get; private set; } = [];

    public static Result<Calificacion> Crear(
        Guid viajeId,
        Guid evaluadorId,
        Guid evaluadoId,
        Rol rolEvaluado,
        int estrellas,
        string? comentario,
        IEnumerable<string> etiquetas)
    {
        if (evaluadorId == evaluadoId)
        {
            return DomainErrors.Calificacion.Autoevaluacion;
        }

        if (estrellas is < 1 or > 5)
        {
            return DomainErrors.Calificacion.EstrellasInvalidas;
        }

        var normalizadas = etiquetas.Select(e => e.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var invalida = normalizadas.FirstOrDefault(e => !EtiquetasCalificacion.EsValida(e));
        if (invalida is not null)
        {
            return DomainErrors.Calificacion.EtiquetaInvalida(invalida);
        }

        return new Calificacion
        {
            ViajeId = viajeId,
            EvaluadorId = evaluadorId,
            EvaluadoId = evaluadoId,
            RolEvaluado = rolEvaluado,
            Estrellas = estrellas,
            Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(),
            Etiquetas = normalizadas.Select(EtiquetasCalificacion.Canonica).ToList()
        };
    }
}

/// <summary>Catálogo de etiquetas de comportamiento mostradas en la pantalla P14.</summary>
public static class EtiquetasCalificacion
{
    public static readonly IReadOnlyList<string> Todas =
    [
        "Puntual",
        "Manejo seguro",
        "Aporte al abordar",
        "Respeto universitario",
        "Buena comunicación",
        "Vehículo limpio",
        "Respeta las reglas"
    ];

    public static bool EsValida(string etiqueta) => Todas.Contains(etiqueta, StringComparer.OrdinalIgnoreCase);

    public static string Canonica(string etiqueta) =>
        Todas.First(e => string.Equals(e, etiqueta, StringComparison.OrdinalIgnoreCase));
}
