using Promotora.Domain.Shared;

namespace Promotora.Domain.Ticketing;

public enum ModalidadMesa { COMPLETA, POR_SILLA }

public sealed record PosicionNumerada(long Id, int TipoEntradaId, long? MesaId, bool Disponible);
public sealed record MesaNumerada(long Id, ModalidadMesa Modalidad, IReadOnlyList<long> AsientoIds);

public static class SeleccionNumerada
{
    public static Result<Unit> Validar(IReadOnlyList<long> seleccion, int tipoEntradaId, int cantidad,
        IReadOnlyList<PosicionNumerada> posiciones, IReadOnlyList<MesaNumerada> mesas)
    {
        if (cantidad <= 0 || seleccion.Count != cantidad || seleccion.Any(id => id <= 0)
            || seleccion.Distinct().Count() != seleccion.Count)
            return Fallo("numeracion.seleccion", "Selecciona una posición diferente por boleto.", ErrorType.Validation);

        var ids = seleccion.ToHashSet();
        var elegidas = posiciones.Where(p => ids.Contains(p.Id)).ToArray();
        if (elegidas.Length != cantidad || elegidas.Any(p => p.TipoEntradaId != tipoEntradaId))
            return Fallo("numeracion.pertenencia", "Las posiciones no corresponden al tipo de entrada.", ErrorType.BusinessRule);
        if (elegidas.Any(p => !p.Disponible))
            return Fallo("numeracion.ocupada", "Una posición ya no está disponible.", ErrorType.Conflict);
        foreach (var mesaId in elegidas.Where(p => p.MesaId.HasValue).Select(p => p.MesaId!.Value).Distinct())
        {
            var mesa = mesas.SingleOrDefault(m => m.Id == mesaId);
            if (mesa is null || !elegidas.Where(p => p.MesaId == mesaId).All(p => mesa.AsientoIds.Contains(p.Id)))
                return Fallo("numeracion.mesa", "La configuración de mesa no es consistente.", ErrorType.BusinessRule);
            if (mesa.Modalidad == ModalidadMesa.COMPLETA && !mesa.AsientoIds.All(ids.Contains))
                return Fallo("numeracion.mesa_completa", "Debes seleccionar todas las sillas de la mesa.", ErrorType.BusinessRule);
        }
        return Result<Unit>.Success(Unit.Value);
    }

    private static Result<Unit> Fallo(string codigo, string mensaje, ErrorType tipo) =>
        Result<Unit>.Failure(new Error(codigo, mensaje, tipo));
}
