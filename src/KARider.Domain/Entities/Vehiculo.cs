using KARider.Domain.Common;

namespace KARider.Domain.Entities;

///Vehículo autorizado de un conductor para carpooling
public sealed class Vehiculo : Entity
{
    public const int CapacidadMaxima = 4;

    private Vehiculo()
    {
    }

    public Guid ConductorId { get; private set; }

    public Usuario? Conductor { get; private set; }

    public string Modelo { get; private set; } = string.Empty;

    public string Color { get; private set; } = string.Empty;

    public int Anio { get; private set; }

    ///Placas normalizadas en mayúsculas.
    public string Placas { get; private set; } = string.Empty;

    /// Asientos autorizados para pasajeros obviamente sin contar al conductor 
    public int Capacidad { get; private set; }

    public bool Verificado { get; private set; }

    public bool Activo { get; private set; } = true;

    public static Vehiculo Crear(Guid conductorId, string modelo, string color, int anio, string placas, int capacidad) => new()
    {
        ConductorId = conductorId,
        Modelo = modelo.Trim(),
        Color = color.Trim(),
        Anio = anio,
        Placas = NormalizarPlacas(placas),
        Capacidad = capacidad
    };

    public static string NormalizarPlacas(string placas) => placas.Trim().ToUpperInvariant();

    public void Actualizar(string modelo, string color, int anio, string placas, int capacidad)
    {
        var nuevasPlacas = NormalizarPlacas(placas);
        if (nuevasPlacas != Placas)
        {
            // Cambiar placas requiere volver a verificar el vehículo.
            Verificado = false;
        }

        Modelo = modelo.Trim();
        Color = color.Trim();
        Anio = anio;
        Placas = nuevasPlacas;
        Capacidad = capacidad;
    }

    public void MarcarVerificado() => Verificado = true;

    public void Desactivar() => Activo = false;
}
