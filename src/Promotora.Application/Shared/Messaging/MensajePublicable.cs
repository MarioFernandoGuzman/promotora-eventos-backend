namespace Promotora.Application.Shared.Messaging;

public sealed record MensajePublicable(long Id, string Tipo, ReadOnlyMemory<byte> Payload);
