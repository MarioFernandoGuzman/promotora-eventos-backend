namespace Promotora.Infrastructure.Reporting;

internal static partial class ReporteCatalogo
{
    static partial void AgregarFinanzas(Dictionary<string, ReporteDefinition> reportes)
    {
        reportes.Add("R-06", new("R-06", "Boletos vendidos y asistencia",
            "Fecha de emisión UTC. Vendidos vigentes = VALIDO + USADO; ANULADO se informa aparte. No se suman diferentes monedas.",
        [new("asistencia", "vw_asistencia_evento", [Dim("evento_id", "eventoId", "Evento ID", "numero"), Dim("evento_nombre", "evento", "Evento"), Moneda],
            [Sum("boletos_emitidos", "emitidos", "Emitidos"), Sum("boletos_vendidos", "vendidos", "Vigentes"), Sum("boletos_usados", "usados", "Usados"), Sum("boletos_anulados", "anulados", "Anulados")], FiltrosMonetarios)]));
        reportes.Add("R-07", new("R-07", "Ingresos por dueño de recinto",
            "Ledger con signo original y fecha del movimiento UTC. Neto incluye liquidaciones. Al filtrar evento/categoría/recinto/estado se excluyen liquidaciones sin compra asociada; no es saldo histórico de la cuenta.",
        [new("ingresos", "vw_ingresos_dueno", [Dim("dueno_id", "duenoId", "Dueño ID", "numero"), Dim("cuenta_id", "cuentaId", "Cuenta ID", "numero"), Moneda],
            [Sum("ventas", "ventas", "Ventas", "dinero"), Sum("reembolsos", "reembolsos", "Reembolsos", "dinero"), Sum("liquidaciones", "liquidaciones", "Liquidaciones", "dinero"), Sum("movimiento_neto", "neto", "Movimiento neto", "dinero")], FiltrosMonetarios)]));
        reportes.Add("R-08", new("R-08", "Comisiones de la plataforma",
            "Ledger PLATAFORMA: comisiones y reembolsos con signo; no incluye IVA. Fecha del movimiento UTC.",
        [new("comisiones", "vw_comisiones_plataforma", [Dim("fecha", "fecha", "Fecha UTC", "fecha"), Moneda],
            [Sum("comisiones", "comisiones", "Comisiones", "dinero"), Sum("reembolsos", "reembolsos", "Reembolsos", "dinero"), Sum("comision_neta", "neto", "Comisión neta", "dinero")], FiltrosMonetarios)]));
        reportes.Add("R-09", new("R-09", "Top clientes", NotaVentas + " Información personal disponible exclusivamente para administración. Orden total descendente y cliente ID como desempate.",
        [new("clientes", "vw_top_clientes", [Dim("cliente_id", "clienteId", "Cliente ID", "numero"), Dim("nombre", "nombre", "Nombre"), Dim("apellido", "apellido", "Apellido"), Moneda],
            [Sum("compras_completadas", "comprasCompletadas", "Compras completadas"), Sum("total", "total", "Total bruto", "dinero")], FiltrosMonetarios,
            Orden: "\"moneda\", \"total\" DESC, \"clienteId\", \"nombre\", \"apellido\"")]));
        reportes.Add("R-10", new("R-10", "Compras fallidas, canceladas y expiradas",
            "Estado filtra compra (FALLIDA/CANCELADA/EXPIRADA). Fecha de compra UTC. Total es importe de intentos, no ingresos cobrados; agrupa por evento, estado y moneda.",
        [new("compras", "vw_compras_no_exitosas", [Dim("evento_id", "eventoId", "Evento ID", "numero"), Dim("compra_estado", "estado", "Estado de compra"), Moneda],
            [new("count(*)", "compras", "Compras"), Sum("intentos_pago", "intentosPago", "Intentos de pago"), Sum("pagos_rechazados", "pagosRechazados", "Pagos rechazados"), Sum("total", "totalIntentado", "Total intentado", "dinero")], FiltrosMonetarios, "compra_estado")]));
    }
}
