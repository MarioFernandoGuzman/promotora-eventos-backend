namespace Promotora.Application.Shared.Ports;

public sealed record CorreoDto(string Destinatario, string Asunto, string Texto, string MessageId);

public interface IEmailSender
{
    Task EnviarAsync(CorreoDto correo, CancellationToken ct);
}

// No contiene dirección, cuerpo ni excepción del proveedor: puede registrarse sin filtrar secretos.
public sealed class CorreoEntregaException : Exception
{
    public CorreoEntregaException() : base("No se pudo entregar el correo al servidor SMTP.") { }
}
