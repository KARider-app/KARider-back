using KARider.Domain.Common;
using KARider.Domain.Enums;

namespace KARider.Domain.Entities;


/// Código de un solo uso verificación de correo o restablecimiento de contraseña que solo almecene el hash

public sealed class CodigoVerificacion : Entity
{
    private CodigoVerificacion()
    {
    }

    public Guid UsuarioId { get; private set; }

    public PropositoCodigo Proposito { get; private set; }

    public string CodigoHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiraEn { get; private set; }

    public int Intentos { get; private set; }

    public DateTimeOffset? UsadoEn { get; private set; }

    public static CodigoVerificacion Crear(Guid usuarioId, PropositoCodigo proposito, string codigoHash, DateTimeOffset expiraEn) => new()
    {
        UsuarioId = usuarioId,
        Proposito = proposito,
        CodigoHash = codigoHash,
        ExpiraEn = expiraEn
    };

    public bool EstaExpirado(DateTimeOffset ahora) => ahora >= ExpiraEn;

    public bool EstaUsado => UsadoEn is not null;

    public void RegistrarIntento() => Intentos++;

    public void MarcarUsado(DateTimeOffset ahora) => UsadoEn = ahora;
}


/// Token de actualización con rotación. Solo se almacena su hash al reutilizar un token revocado
/// se revocan todas las sesiones del usuario detección de robo

public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public Guid UsuarioId { get; private set; }

    public Usuario? Usuario { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiraEn { get; private set; }

    public DateTimeOffset? RevocadoEn { get; private set; }

    public Guid? ReemplazadoPorId { get; private set; }

    public string? IpCreacion { get; private set; }

    public bool Recordarme { get; private set; }

    public static RefreshToken Crear(Guid usuarioId, string tokenHash, DateTimeOffset expiraEn, string? ip, bool recordarme) => new()
    {
        UsuarioId = usuarioId,
        TokenHash = tokenHash,
        ExpiraEn = expiraEn,
        IpCreacion = ip,
        Recordarme = recordarme
    };

    public bool EstaActivo(DateTimeOffset ahora) => RevocadoEn is null && ahora < ExpiraEn;

    public void Revocar(DateTimeOffset ahora, Guid? reemplazadoPor = null)
    {
        RevocadoEn ??= ahora;
        ReemplazadoPorId = reemplazadoPor;
    }
}

//Notificación persistida que además se envía como push.
public sealed class Notificacion : Entity
{
    private Notificacion()
    {
    }

    public Guid UsuarioId { get; private set; }

    public TipoNotificacion Tipo { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string Mensaje { get; private set; } = string.Empty;

    public Guid? ViajeId { get; private set; }

    public Guid? ReservaId { get; private set; }

    public bool Leida { get; private set; }

    public DateTimeOffset? LeidaEn { get; private set; }

    public static Notificacion Crear(Guid usuarioId, TipoNotificacion tipo, string titulo, string mensaje, Guid? viajeId, Guid? reservaId) => new()
    {
        UsuarioId = usuarioId,
        Tipo = tipo,
        Titulo = titulo,
        Mensaje = mensaje,
        ViajeId = viajeId,
        ReservaId = reservaId
    };

    public void MarcarLeida(DateTimeOffset ahora)
    {
        if (!Leida)
        {
            Leida = true;
            LeidaEn = ahora;
        }
    }
}

/// <summary>Suscripción Web Push (VAPID) de un dispositivo del usuario.</summary>
public sealed class SuscripcionPush : Entity
{
    private SuscripcionPush()
    {
    }

    public Guid UsuarioId { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public string P256dh { get; private set; } = string.Empty;

    public string Auth { get; private set; } = string.Empty;

    public static SuscripcionPush Crear(Guid usuarioId, string endpoint, string p256dh, string auth) => new()
    {
        UsuarioId = usuarioId,
        Endpoint = endpoint,
        P256dh = p256dh,
        Auth = auth
    };

    public void Actualizar(Guid usuarioId, string p256dh, string auth)
    {
        UsuarioId = usuarioId;
        P256dh = p256dh;
        Auth = auth;
    }
}
