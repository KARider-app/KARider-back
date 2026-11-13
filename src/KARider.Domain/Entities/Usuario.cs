using KARider.Domain.Common;
using KARider.Domain.Enums;

namespace KARider.Domain.Entities;


// Estudiante de la UTTT pasajero o conductor Un conductor también puede viajar como pasajero.

public sealed class Usuario : Entity
{
    private readonly List<Vehiculo> _vehiculos = [];

    private Usuario()
    {
    }

    public string NombreCompleto { get; private set; } = string.Empty;

    public string Matricula { get; private set; } = string.Empty;

    public string Telefono { get; private set; } = string.Empty;

    ///Correo institucional normalizado en minúsculas
    public string CorreoInstitucional { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public Rol Rol { get; private set; }

    public int CarreraId { get; private set; }

    public Carrera? Carrera { get; private set; }

    public int? Cuatrimestre { get; private set; }

    public string? FotoUrl { get; private set; }

    public bool CorreoVerificado { get; private set; }

    public DateTimeOffset? CorreoVerificadoEn { get; private set; }

    public bool Activo { get; private set; } = true;

    public decimal CalificacionPromedio { get; private set; }

    public int TotalCalificaciones { get; private set; }

    public int IntentosFallidos { get; private set; }

    public DateTimeOffset? BloqueadoHasta { get; private set; }

    ///Cambia cuando se modifica la contraseña; permite invalidar sesiones anteriores.
    public string SecurityStamp { get; private set; } = Guid.NewGuid().ToString("N");

    public IReadOnlyCollection<Vehiculo> Vehiculos => _vehiculos;

    public bool EsConductor => Rol is Rol.Conductor or Rol.Administrador;

    public static Usuario Crear(
        string nombreCompleto,
        string matricula,
        string telefono,
        string correoInstitucional,
        string passwordHash,
        Rol rol,
        int carreraId,
        int? cuatrimestre) => new()
        {
            NombreCompleto = nombreCompleto.Trim(),
            Matricula = matricula.Trim(),
            Telefono = telefono.Trim(),
            CorreoInstitucional = NormalizarCorreo(correoInstitucional),
            PasswordHash = passwordHash,
            Rol = rol,
            CarreraId = carreraId,
            Cuatrimestre = cuatrimestre
        };

    public static string NormalizarCorreo(string correo) => correo.Trim().ToLowerInvariant();

    public void AgregarVehiculo(Vehiculo vehiculo)
    {
        _vehiculos.Add(vehiculo);
        PromoverAConductor();
    }

    public void PromoverAConductor()
    {
        if (Rol == Rol.Pasajero)
        {
            Rol = Rol.Conductor;
        }
    }

    public void MarcarCorreoVerificado(DateTimeOffset ahora)
    {
        CorreoVerificado = true;
        CorreoVerificadoEn = ahora;
    }

    public bool EstaBloqueado(DateTimeOffset ahora) => BloqueadoHasta is not null && BloqueadoHasta > ahora;

    /// <summary>Registra un intento fallido y bloquea la cuenta al alcanzar el máximo permitido.</summary>
    public void RegistrarIntentoFallido(DateTimeOffset ahora, int maximoIntentos, TimeSpan duracionBloqueo)
    {
        IntentosFallidos++;
        if (IntentosFallidos >= maximoIntentos)
        {
            BloqueadoHasta = ahora.Add(duracionBloqueo);
            IntentosFallidos = 0;
        }
    }

    public void ReiniciarIntentosFallidos()
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    public void CambiarPassword(string nuevoHash)
    {
        PasswordHash = nuevoHash;
        SecurityStamp = Guid.NewGuid().ToString("N");
        ReiniciarIntentosFallidos();
    }

    /// <summary>Actualiza el hash cuando el algoritmo cambia, sin invalidar sesiones.</summary>
    public void RehashPassword(string nuevoHash) => PasswordHash = nuevoHash;

    public void ActualizarPerfil(string telefono, int? cuatrimestre, string? fotoUrl)
    {
        Telefono = telefono.Trim();
        Cuatrimestre = cuatrimestre;
        FotoUrl = string.IsNullOrWhiteSpace(fotoUrl) ? null : fotoUrl.Trim();
    }

    /// <summary>Recalcula el promedio de manera incremental al recibir una nueva calificación.</summary>
    public void AplicarCalificacion(int estrellas)
    {
        var total = (CalificacionPromedio * TotalCalificaciones) + estrellas;
        TotalCalificaciones++;
        CalificacionPromedio = Math.Round(total / TotalCalificaciones, 2, MidpointRounding.AwayFromZero);
    }

    public void Desactivar() => Activo = false;
}
