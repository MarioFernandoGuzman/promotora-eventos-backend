using System.Text.Json;

namespace Promotora.Application.Shared.Messaging;

public sealed record NuevoMensajeOutbox(string Agregado, string AgregadoId, string Tipo, JsonElement Payload);

public sealed record ResultadoRelayOutbox(int Publicados, int Fallidos);
