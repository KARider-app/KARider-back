using KARider.Domain.Common;
using KARider.Domain.Enums;
using KARider.Domain.Errors;

namespace KARider.Domain.Entities;


/// Viaje publicado por un conductor desde un punto de encuentro del campus hacia un destino.

public sealed class Viaje : Entity
{
    private readonly List<ParadaViaje> _paradas = [];
    private readonly List<Reserva> _reservas = [];

    private Viaje()
    {
    }

    public Guid ConductorId { get; private set; }

    public Usuario? Conductor { get; private set; }

    public Guid VehiculoId { get; private set; }

    public Vehiculo? Vehiculo { get; private set; }

    public int PuntoEncuentroId { get; private set; }

    public PuntoEncuentro? PuntoEncuentro { get; private set; }

    public string Destino { get; private set; } = string.Empty;

    public double? DestinoLatitud { get; private set; }

    public double? DestinoLongitud { get; private set; }

    public DateTimeOffset FechaSalida { get; private set; }

    public int AsientosOfrecidos { get; private set; }

    public int AsientosDisponibles { get; private set; }

    /// <summary>Gasto total estimado del trayecto (gasolina + casetas) en MXN.</summary>
    public decimal GastoTotal { get; private set; }

    /// <summary>Aporte solidario por asiento: gasto dividido entre pasajeros + conductor.</summary>
    public decimal AportePorAsiento { get; private set; }

    public string? Notas { get; private set; }

    public EstadoViaje Estado { get; private set; }

    public double? UbicacionLatitud { get; private set; }

    public double? UbicacionLongitud { get; private set; }

    public DateTimeOffset? UbicacionActualizadaEn { get; private set; }

    /// <summary>Veces que el conductor ha editado el viaje después de crearlo (HU-09).</summary>
    public int NumeroEdiciones { get; private set; }

    public IReadOnlyCollection<ParadaViaje> Paradas => _paradas;

    public IReadOnlyCollection<Reserva> Reservas => _reservas;

    public int AsientosReservados => AsientosOfrecidos - AsientosDisponibles;

    /// <summary>
    /// Divide el gasto entre los asientos ofrecidos más el conductor, quien también aporta su parte.
    /// Ej. $160 con 3 asientos => $160 / 4 personas = $40 por asiento.
    /// </summary>
    public static decimal CalcularAporte(decimal gastoTotal, int asientos)
    {
        if (asientos <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(asientos), "El número de asientos debe ser mayor a cero.");
        }

        return Math.Round(gastoTotal / (asientos + 1), 2, MidpointRounding.AwayFromZero);
    }

    public static Result<Viaje> Crear(
        Guid conductorId,
        Vehiculo vehiculo,
        int puntoEncuentroId,
        string destino,
        double? destinoLatitud,
        double? destinoLongitud,
        DateTimeOffset fechaSalida,
        int asientos,
        decimal gastoTotal,
        string? notas,
        IEnumerable<string> paradas,
        bool publicar,
        DateTimeOffset ahora)
    {
        if (fechaSalida <= ahora)
        {
            return DomainErrors.Viaje.FechaPasada;
        }

        if (asientos > vehiculo.Capacidad)
        {
            return DomainErrors.Vehiculo.CapacidadInsuficiente(asientos, vehiculo.Capacidad);
        }

        var viaje = new Viaje
        {
            ConductorId = conductorId,
            VehiculoId = vehiculo.Id,
            PuntoEncuentroId = puntoEncuentroId,
            Destino = destino.Trim(),
            DestinoLatitud = destinoLatitud,
            DestinoLongitud = destinoLongitud,
            FechaSalida = fechaSalida.ToUniversalTime(),
            AsientosOfrecidos = asientos,
            AsientosDisponibles = asientos,
            GastoTotal = gastoTotal,
            AportePorAsiento = CalcularAporte(gastoTotal, asientos),
            Notas = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim(),
            Estado = publicar ? EstadoViaje.Programado : EstadoViaje.Borrador
        };

        var orden = 1;
        foreach (var parada in paradas.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            viaje._paradas.Add(ParadaViaje.Crear(viaje.Id, orden++, parada));
        }

        return viaje;
    }

    public bool AceptaReservas(DateTimeOffset ahora) => Estado == EstadoViaje.Programado && FechaSalida > ahora;

    public bool EsEditable => Estado is EstadoViaje.Borrador or EstadoViaje.Programado;

    public Result ApartarAsientos(int asientos, DateTimeOffset ahora)
    {
        if (!AceptaReservas(ahora))
        {
            return DomainErrors.Viaje.NoDisponible;
        }

        if (asientos > AsientosDisponibles)
        {
            return DomainErrors.Viaje.SinCupo(AsientosDisponibles);
        }

        AsientosDisponibles -= asientos;
        return Result.Success();
    }

    public void LiberarAsientos(int asientos) =>
        AsientosDisponibles = Math.Min(AsientosOfrecidos, AsientosDisponibles + asientos);

    /// <summary>Edición del viaje publicado (pantalla P12): mismos datos que al publicarlo, salvo el vehículo.</summary>
    public Result Actualizar(
        int puntoEncuentroId,
        string destino,
        double? destinoLatitud,
        double? destinoLongitud,
        DateTimeOffset fechaSalida,
        int asientosOfrecidos,
        decimal gastoTotal,
        string? notas,
        IEnumerable<string> paradas,
        int capacidadVehiculo,
        DateTimeOffset ahora)
    {
        if (!EsEditable)
        {
            return DomainErrors.Viaje.NoEditable(Estado);
        }

        if (fechaSalida <= ahora)
        {
            return DomainErrors.Viaje.FechaPasada;
        }

        var reservados = AsientosReservados;
        if (asientosOfrecidos < reservados)
        {
            return DomainErrors.Viaje.AsientosInsuficientes(reservados);
        }

        if (asientosOfrecidos > capacidadVehiculo)
        {
            return DomainErrors.Vehiculo.CapacidadInsuficiente(asientosOfrecidos, capacidadVehiculo);
        }

        PuntoEncuentroId = puntoEncuentroId;
        Destino = destino.Trim();
        DestinoLatitud = destinoLatitud;
        DestinoLongitud = destinoLongitud;
        FechaSalida = fechaSalida.ToUniversalTime();
        AsientosOfrecidos = asientosOfrecidos;
        AsientosDisponibles = asientosOfrecidos - reservados;
        GastoTotal = gastoTotal;
        AportePorAsiento = CalcularAporte(gastoTotal, asientosOfrecidos);
        Notas = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim();

        _paradas.Clear();
        var orden = 1;
        foreach (var parada in paradas.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            _paradas.Add(ParadaViaje.Crear(Id, orden++, parada));
        }

        NumeroEdiciones++;
        return Result.Success();
    }

    /// <summary>Aplica la máquina de estados del viaje y propaga el cambio a sus reservas.</summary>
    public Result CambiarEstado(EstadoViaje nuevo, DateTimeOffset ahora)
    {
        var permitido = (Estado, nuevo) switch
        {
            (EstadoViaje.Borrador, EstadoViaje.Programado) => true,
            (EstadoViaje.Borrador, EstadoViaje.Cancelado) => true,
            (EstadoViaje.Programado, EstadoViaje.EnCurso) => true,
            (EstadoViaje.Programado, EstadoViaje.Cancelado) => true,
            (EstadoViaje.EnCurso, EstadoViaje.Completado) => true,
            _ => false
        };

        if (!permitido)
        {
            return DomainErrors.Viaje.TransicionInvalida(Estado, nuevo);
        }

        if (nuevo == EstadoViaje.Programado && FechaSalida <= ahora)
        {
            return DomainErrors.Viaje.FechaPasada;
        }

        Estado = nuevo;

        foreach (var reserva in _reservas)
        {
            switch (nuevo)
            {
                case EstadoViaje.EnCurso when reserva.Estado == EstadoReserva.Pendiente:
                case EstadoViaje.Completado when reserva.Estado == EstadoReserva.Pendiente:
                    reserva.Expirar(ahora);
                    break;
                case EstadoViaje.Completado when reserva.Estado == EstadoReserva.Confirmada:
                    reserva.Completar(ahora);
                    break;
                case EstadoViaje.Cancelado when reserva.EstaActiva:
                    reserva.CancelarPorViaje(ahora);
                    break;
            }
        }

        if (nuevo is EstadoViaje.Completado or EstadoViaje.Cancelado)
        {
            UbicacionLatitud = null;
            UbicacionLongitud = null;
            UbicacionActualizadaEn = null;
        }

        return Result.Success();
    }

    public Result ActualizarUbicacion(double latitud, double longitud, DateTimeOffset ahora)
    {
        if (Estado is not (EstadoViaje.Programado or EstadoViaje.EnCurso))
        {
            return DomainErrors.Viaje.UbicacionNoPermitida;
        }

        UbicacionLatitud = latitud;
        UbicacionLongitud = longitud;
        UbicacionActualizadaEn = ahora;
        return Result.Success();
    }
}

/// <summary>Parada intermedia del itinerario.</summary>
public sealed class ParadaViaje
{
    private ParadaViaje()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid ViajeId { get; private set; }

    public int Orden { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public static ParadaViaje Crear(Guid viajeId, int orden, string nombre) => new()
    {
        ViajeId = viajeId,
        Orden = orden,
        Nombre = nombre.Trim()
    };
}
