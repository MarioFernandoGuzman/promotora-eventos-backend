namespace Promotora.Domain.Reviews;

public static class CalificacionMedia
{
    public static decimal? Calcular(long suma, long cantidad)
    {
        if (cantidad < 0 || suma < cantidad || suma > (decimal)cantidad * 5)
            throw new ArgumentOutOfRangeException(nameof(suma), "El agregado debe corresponder a calificaciones 1–5.");
        return cantidad == 0 ? null : decimal.Round((decimal)suma / cantidad, 2, MidpointRounding.AwayFromZero);
    }
}
