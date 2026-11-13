using KARider.Domain.Common;

namespace KARider.Domain.Entities;

/// <summary>
/// Registro de cada edición de un viaje publicado (HU-09, pantalla P12). Conserva los valores
/// anteriores y nuevos para que el conductor y los pasajeros puedan consultar qué cambió.
/// </summary>
public sealed class HistorialEdicionViaje : Entity
{
    private HistorialEdicionViaje()
    {
    }

    public Guid ViajeId { get; private set; }

    public DateTimeOffset FechaSalidaAnterior { get; private set; }

    public DateTimeOffset FechaSalidaNueva { get; private set; }

    public int PuntoEncuentroAnteriorId { get; private set; }

    public int PuntoEncuentroNuevoId { get; private set; }

    public int CuposLibresAnteriores { get; private set; }

    public int CuposLibresNuevos { get; private set; }

    public string? MensajeAviso { get; private set; }

    public static HistorialEdicionViaje Registrar(
        Guid viajeId,
        DateTimeOffset fechaSalidaAnterior,
        int puntoEncuentroAnteriorId,
        int cuposLibresAnteriores,
        Viaje viajeActualizado,
        string? mensajeAviso) => new()
    {
        ViajeId = viajeId,
        FechaSalidaAnterior = fechaSalidaAnterior,
        FechaSalidaNueva = viajeActualizado.FechaSalida,
        PuntoEncuentroAnteriorId = puntoEncuentroAnteriorId,
        PuntoEncuentroNuevoId = viajeActualizado.PuntoEncuentroId,
        CuposLibresAnteriores = cuposLibresAnteriores,
        CuposLibresNuevos = viajeActualizado.AsientosDisponibles,
        MensajeAviso = string.IsNullOrWhiteSpace(mensajeAviso) ? null : mensajeAviso.Trim()
    };
}
