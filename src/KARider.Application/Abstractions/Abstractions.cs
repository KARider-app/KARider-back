using KARider.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KARider.Application.Abstractions;

/// <summary>
/// Puerto de persistencia. La implementación (EF Core + PostgreSQL) vive en Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Usuario> Usuarios { get; }
    DbSet<Carrera> Carreras { get; }
    DbSet<PuntoEncuentro> PuntosEncuentro { get; }
    DbSet<Vehiculo> Vehiculos { get; }
    DbSet<Viaje> Viajes { get; }
    DbSet<ParadaViaje> ParadasViaje { get; }
    DbSet<HistorialEdicionViaje> HistorialEdicionesViaje { get; }
    DbSet<Reserva> Reservas { get; }
    DbSet<Calificacion> Calificaciones { get; }
    DbSet<CodigoVerificacion> CodigosVerificacion { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Notificacion> Notificaciones { get; }
    DbSet<SuscripcionPush> SuscripcionesPush { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public enum PasswordVerification
{
    Failed,
    Success,
    SuccessRehashNeeded
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string hashedPassword, string providedPassword);
}

/// <summary>Genera y verifica secretos de un solo uso (códigos y refresh tokens) usando HMAC con pepper.</summary>
public interface ISecretHasher
{
    string Hash(string value);

    bool Verify(string value, string hash);

    string GenerateNumericCode(int digits);

    string GenerateToken();
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiraEn);

public interface ITokenService
{
    AccessToken CreateAccessToken(Usuario usuario);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public sealed record PushMessage(string Titulo, string Mensaje, string Tipo, Guid? ViajeId, Guid? ReservaId);

public interface IPushSender
{
    /// <summary>Clave pública VAPID para que el frontend se suscriba (null si push no está configurado).</summary>
    string? PublicKey { get; }

    /// <summary>Envía la notificación y devuelve los endpoints expirados que deben eliminarse.</summary>
    Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<SuscripcionPush> suscripciones,
        PushMessage message,
        CancellationToken cancellationToken = default);
}
