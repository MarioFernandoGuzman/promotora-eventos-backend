using Promotora.Domain.Ticketing;

namespace Promotora.Domain.Tests;

public sealed class SeleccionNumeradaTests
{
    [Fact]
    public void MesaCompletaExigeTodasLasSillasYVentaIndividualPermiteParte()
    {
        PosicionNumerada[] sillas = [new(1, 10, 7, true), new(2, 10, 7, true)];
        Assert.True(SeleccionNumerada.Validar([1], 10, 1, sillas, [new(7, ModalidadMesa.COMPLETA, [1, 2])]).IsFailure);
        Assert.True(SeleccionNumerada.Validar([1, 2], 10, 2, sillas, [new(7, ModalidadMesa.COMPLETA, [1, 2])]).IsSuccess);
        Assert.True(SeleccionNumerada.Validar([1], 10, 1, sillas, [new(7, ModalidadMesa.POR_SILLA, [1, 2])]).IsSuccess);
    }

    [Fact]
    public void NoAdmiteDuplicadosPosicionesAjenasNiSillasOcupadas()
    {
        PosicionNumerada[] sillas = [new(1, 10, null, true), new(2, 10, null, false)];
        Assert.True(SeleccionNumerada.Validar([1, 1], 10, 2, sillas, []).IsFailure);
        Assert.True(SeleccionNumerada.Validar([1], 11, 1, sillas, []).IsFailure);
        Assert.True(SeleccionNumerada.Validar([2], 10, 1, sillas, []).IsFailure);
    }
}
