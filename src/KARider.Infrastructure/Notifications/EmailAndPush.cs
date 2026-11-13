using System.Net;
using System.Net.Mail;
using System.Text.Json;
using KARider.Application.Abstractions;
using KARider.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace KARider.Infrastructure.Notifications;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public string? Usuario { get; set; }

    public string? Password { get; set; }

    public string Remitente { get; set; } = "no-reply@karider.app";

    public string NombreRemitente { get; set; } = "KARider UTTT";

    public bool Configurado => !string.IsNullOrWhiteSpace(Host);
}

public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>mailto: o URL de contacto requerida por VAPID.</summary>
    public string Subject { get; set; } = "mailto:soporte@karider.app";

    public string? PublicKey { get; set; }

    public string? PrivateKey { get; set; }

    public bool Configurado => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var mensaje = new MailMessage
        {
            From = new MailAddress(_options.Remitente, _options.NombreRemitente),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        mensaje.To.Add(to);

        using var cliente = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = string.IsNullOrWhiteSpace(_options.Usuario)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.Usuario, _options.Password)
        };

        await cliente.SendMailAsync(mensaje, cancellationToken);
    }
}

/// <summary>
/// Sustituto cuando no hay SMTP configurado. En Development escribe el correo en el log
/// (para probar la verificación); en otros entornos nunca registra el contenido.
/// </summary>
internal sealed class LogEmailSender(IHostEnvironment environment, ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (environment.IsDevelopment())
        {
            logger.LogInformation("[DEV] Correo para {Destinatario} · {Asunto}\n{Cuerpo}", to, subject, htmlBody);
        }
        else
        {
            logger.LogError("SMTP no configurado: no se envió el correo «{Asunto}». Configura Smtp__Host.", subject);
        }

        return Task.CompletedTask;
    }
}

internal sealed class WebPushSender(IOptions<WebPushOptions> options, ILogger<WebPushSender> logger) : IPushSender, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebPushOptions _options = options.Value;
    private readonly WebPushClient _client = new();

    public string? PublicKey => _options.Configurado ? _options.PublicKey : null;

    public async Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<SuscripcionPush> suscripciones,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Configurado)
        {
            return [];
        }

        var vapid = new VapidDetails(_options.Subject, _options.PublicKey, _options.PrivateKey);
        var payload = JsonSerializer.Serialize(message, JsonOptions);
        var expirados = new List<string>();

        foreach (var suscripcion in suscripciones)
        {
            try
            {
                var destino = new PushSubscription(suscripcion.Endpoint, suscripcion.P256dh, suscripcion.Auth);
                await _client.SendNotificationAsync(destino, payload, vapid, cancellationToken);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                expirados.Add(suscripcion.Endpoint);
            }
            catch (WebPushException ex)
            {
                logger.LogWarning(ex, "Fallo al enviar push ({Status}) a la suscripción {SuscripcionId}", ex.StatusCode, suscripcion.Id);
            }
        }

        return expirados;
    }

    public void Dispose() => _client.Dispose();
}
