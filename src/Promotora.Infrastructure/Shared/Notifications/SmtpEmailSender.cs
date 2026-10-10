using System.Net;
using System.Net.Mail;
using System.Text;
using Promotora.Application.Shared.Ports;

namespace Promotora.Infrastructure.Shared.Notifications;

public sealed record SmtpOptions(string Host, int Port, string From, bool EnableSsl = false, string? Username = null,
    string? Password = null, int TimeoutSeconds = 15)
{
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Host) || Port is < 1 or > 65535 || !MailAddress.TryCreate(From, out _)
            || TimeoutSeconds is < 1 or > 120 || (string.IsNullOrEmpty(Username) != string.IsNullOrEmpty(Password)))
            throw new ArgumentException("Configuración SMTP externa inválida.");
        if (!string.IsNullOrEmpty(Username) && !EnableSsl) throw new ArgumentException("SMTP autenticado requiere TLS.");
    }
}

public sealed class SmtpEmailSender(SmtpOptions options) : IEmailSender
{
    public async Task EnviarAsync(CorreoDto correo, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        options.Validar();
        try
        {
            using var mail = new MailMessage(options.From, correo.Destinatario, correo.Asunto, correo.Texto)
            { IsBodyHtml = false, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8 };
            mail.Headers.Add("Message-ID", $"<{correo.MessageId}>");
            using var smtp = new SmtpClient(options.Host, options.Port)
            {
                EnableSsl = options.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = string.IsNullOrEmpty(options.Username) ? null : new NetworkCredential(options.Username, options.Password)
            };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            await smtp.SendMailAsync(mail, timeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is SmtpException or FormatException or OperationCanceledException)
        { throw new CorreoEntregaException(); }
    }
}
