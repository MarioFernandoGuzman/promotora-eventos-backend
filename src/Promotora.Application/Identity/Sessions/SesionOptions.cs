namespace Promotora.Application.Identity.Sessions;

public sealed record SesionOptions(int AccessMinutes = 15, int RefreshDays = 7);
