using System.Globalization;
using ClosedXML.Excel;
using Promotora.Application.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Promotora.Infrastructure.Reporting;

public sealed class ReportExporter : IReportExporter
{
    static ReportExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public async Task<ArchivoReporteDto> ExportarAsync(ReporteDto reporte, string formato, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Archivo efímero con nombre aleatorio, acceso exclusivo y borrado al cerrar.
        // Se entrega como stream, sin duplicar el XLSX/PDF en un byte[] de respuesta.
        var stream = new FileStream(Path.Combine(Path.GetTempPath(), $"promotora-reporte-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            if (formato == "xlsx") Excel(reporte, stream, ct);
            else if (formato == "pdf") Pdf(reporte, stream, ct);
            else throw new ArgumentOutOfRangeException(nameof(formato));
            await stream.FlushAsync(ct);
            stream.Position = 0;
            return new(stream, formato == "xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf", $"{reporte.Codigo}.{formato}");
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private static void Excel(ReporteDto reporte, Stream stream, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var info = workbook.Worksheets.Add("Informacion");
        info.Cell(1, 1).Value = reporte.Codigo; info.Cell(1, 2).Value = reporte.Titulo;
        info.Cell(2, 1).Value = "Interpretación"; info.Cell(2, 2).Value = reporte.Nota;
        info.Cell(3, 1).Value = "Filtros"; info.Cell(3, 2).Value = Filtros(reporte);
        info.Column(1).Width = 22; info.Column(2).Width = 100; info.Column(2).Style.Alignment.WrapText = true;
        info.Row(2).Height = 90; info.Row(3).Height = 45;
        foreach (var seccion in reporte.Secciones)
        {
            ct.ThrowIfCancellationRequested();
            var sheet = workbook.Worksheets.Add(seccion.Codigo);
            int row = 1;
            foreach (var columna in seccion.Columnas.Select((c, i) => (c, i)))
            {
                sheet.Cell(row, columna.i + 1).Value = columna.c.Titulo;
                sheet.Column(columna.i + 1).Width = columna.c.Tipo == "texto" ? 28 : 19;
                sheet.Column(columna.i + 1).Style.NumberFormat.Format = columna.c.Tipo switch
                {
                    "dinero" or "decimal" or "porcentaje" => "0.00",
                    "numero" => "0",
                    "fecha" => "yyyy-mm-dd",
                    _ => "@"
                };
            }
            sheet.Row(1).Style.Font.Bold = true;
            sheet.SheetView.FreezeRows(1);
            foreach (var fila in seccion.Filas)
            {
                ct.ThrowIfCancellationRequested(); row++;
                for (int i = 0; i < seccion.Columnas.Count; i++)
                    PonerValor(sheet.Cell(row, i + 1), fila.GetValueOrDefault(seccion.Columnas[i].Campo));
            }
            if (row > 1) sheet.Range(1, 1, row, seccion.Columnas.Count).SetAutoFilter();
            row += 2; sheet.Cell(row++, 1).Value = "TOTALES DEL CONJUNTO FILTRADO (POR MONEDA SI APLICA)";
            foreach (var total in seccion.Totales)
            {
                for (int i = 0; i < seccion.Columnas.Count; i++)
                    PonerValor(sheet.Cell(row, i + 1), total.GetValueOrDefault(seccion.Columnas[i].Campo));
                sheet.Row(row++).Style.Font.Bold = true;
            }
        }
        workbook.SaveAs(stream);
    }

    private static void PonerValor(IXLCell cell, object? value)
    {
        // Texto siempre como valor: ninguna cadena del negocio se evalúa como fórmula.
        switch (value)
        {
            case null: break;
            case DateOnly fecha: cell.Value = fecha.ToDateTime(TimeOnly.MinValue); break;
            case DateTime fecha: cell.Value = fecha; break;
            case decimal numero: cell.Value = numero; break;
            case long numero: cell.Value = numero; break;
            case int numero: cell.Value = numero; break;
            default: cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""; break;
        }
    }

    private static void Pdf(ReporteDto reporte, Stream stream, CancellationToken ct)
    {
        Document.Create(document =>
        {
            foreach (var seccion in reporte.Secciones)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A3.Landscape()); page.Margin(24); page.DefaultTextStyle(x => x.FontSize(9));
                    page.Header().Column(c =>
                    {
                        c.Item().Text($"{reporte.Codigo} · {reporte.Titulo}").Bold().FontSize(16);
                        c.Item().Text(reporte.Nota).FontSize(9);
                        c.Item().Text(Filtros(reporte)).FontSize(9);
                        c.Item().PaddingVertical(6).Text($"Sección {seccion.Codigo} · {seccion.TotalFilas} filas").Bold();
                    });
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns => { foreach (var _ in seccion.Columnas) columns.RelativeColumn(); });
                        table.Header(header => { foreach (var column in seccion.Columnas) header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text(column.Titulo).Bold(); });
                        foreach (var fila in seccion.Filas)
                        {
                            ct.ThrowIfCancellationRequested();
                            foreach (var column in seccion.Columnas)
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(Texto(fila.GetValueOrDefault(column.Campo), column.Tipo));
                        }
                        foreach (var total in seccion.Totales)
                            foreach (var column in seccion.Columnas)
                                table.Cell().Background(Colors.Blue.Lighten5).Padding(5).Text(total.ContainsKey(column.Campo) ? Texto(total[column.Campo], column.Tipo) : "").Bold();
                    });
                    page.Footer().AlignRight().Text(t => { t.Span("Totales del conjunto filtrado · Página "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
                });
            }
        }).GeneratePdf(stream);
    }

    private static string Texto(object? value, string tipo) => value switch
    {
        null => "—",
        DateOnly fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal numero => numero.ToString(tipo == "numero" ? "0" : "0.00", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };
    private static string Filtros(ReporteDto reporte)
    {
        var f = reporte.Filtros;
        return System.Text.Json.JsonSerializer.Serialize(new { f.Desde, f.Hasta, f.EventoId, f.CategoriaId, f.RecintoId, f.DuenoId, f.Estado, f.Moneda, f.Metodo });
    }
}
