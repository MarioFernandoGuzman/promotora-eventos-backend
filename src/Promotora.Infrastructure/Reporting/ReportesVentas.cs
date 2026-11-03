namespace Promotora.Infrastructure.Reporting;

internal static partial class ReporteCatalogo
{
    private static CampoReporte Dim(string columna, string campo, string titulo, string tipo = "texto") => new(columna, campo, titulo, tipo);
    private static CampoReporte Moneda => Dim("moneda", "moneda", "Moneda");
    private static MetricaReporte Sum(string columna, string campo, string titulo, string tipo = "numero") => new($"coalesce(sum({columna}), 0)", campo, titulo, tipo);
    private static readonly string[] FiltrosEvento = ["desde", "hasta", "evento_id", "categoria_id", "recinto_id", "dueno_id", "estado"];
    private static readonly string[] FiltrosMonetarios = [.. FiltrosEvento, "moneda"];
    private static MetricaReporte[] ImportesVenta =>
    [
        Sum("compras_completadas", "comprasCompletadas", "Compras completadas"),
        Sum("boletos_vendidos", "boletosVendidos", "Boletos vendidos brutos"),
        Sum("subtotal", "subtotal", "Subtotal", "dinero"), Sum("comision_plataforma", "comision", "Comisión", "dinero"),
        Sum("impuestos", "impuestos", "IVA", "dinero"), Sum("total", "total", "Total bruto", "dinero")
    ];
    private const string NotaVentas = "Ventas brutas de compras COMPLETADA, incluso reembolsadas. Para ingresos netos consultar R-07/R-08. Fechas de compra en UTC; monedas separadas.";

    static partial void AgregarVentas(Dictionary<string, ReporteDefinition> reportes)
    {
        reportes.Add("R-01", new("R-01", "Ventas por evento", NotaVentas,
        [new("ventas", "vw_ventas_por_evento", [Dim("evento_id", "eventoId", "Evento ID", "numero"), Dim("evento_nombre", "evento", "Evento"), Moneda], ImportesVenta, FiltrosMonetarios)]));
        reportes.Add("R-02", new("R-02", "Ventas por categoría", NotaVentas,
        [new("ventas", "vw_ventas_por_categoria", [Dim("categoria_id", "categoriaId", "Categoría ID", "numero"), Dim("categoria_nombre", "categoria", "Categoría"), Moneda], ImportesVenta, FiltrosMonetarios)]));
        reportes.Add("R-03", new("R-03", "Ventas por recinto", NotaVentas,
        [new("ventas", "vw_ventas_por_recinto", [Dim("recinto_id", "recintoId", "Recinto ID", "numero"), Dim("recinto_nombre", "recinto", "Recinto"), Moneda], ImportesVenta, FiltrosMonetarios)]));
        reportes.Add("R-04", new("R-04", "Ventas por fecha y método de pago", NotaVentas + " Último pago APROBADO por compra; método vacío si no existe (por ejemplo, compra gratuita).",
        [new("ventas", "vw_ventas_diarias", [Dim("fecha", "fecha", "Fecha UTC", "fecha"), Dim("metodo", "metodo", "Método"), Moneda],
            [Sum("compras_completadas", "comprasCompletadas", "Compras completadas"), Sum("total", "total", "Total bruto", "dinero")], [.. FiltrosMonetarios, "metodo"])]));
        reportes.Add("R-05", new("R-05", "Ocupación por localidad",
            "Cupo ocupado incluye reservas pendientes, ventas y posiciones bloqueadas. Fecha del inicio del evento UTC. Porcentaje ponderado por cupo; no mide asistencia. Sin filtro de moneda.",
        [new("ocupacion", "vw_ocupacion_localidad",
            [Dim("evento_id", "eventoId", "Evento ID", "numero"), Dim("evento_nombre", "evento", "Evento"), Dim("localidad_id", "localidadId", "Localidad ID", "numero"), Dim("localidad_nombre", "localidad", "Localidad")],
            [Sum("cupo_total", "cupoTotal", "Cupo total"), Sum("cupo_disponible", "cupoDisponible", "Cupo disponible"), Sum("cupo_ocupado", "cupoOcupado", "Cupo ocupado"),
                new("round(100.0 * sum(cupo_ocupado) / nullif(sum(cupo_total), 0), 2)", "porcentajeOcupacion", "Ocupación %", "porcentaje")], FiltrosEvento)]));
    }
}
