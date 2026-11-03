using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Promotora.Application.Reporting;
using Promotora.Domain.Shared;
using Promotora.Infrastructure.Shared.Persistence;

namespace Promotora.Infrastructure.Reporting;

public sealed class SqlReporteRepository(PromotoraDbContext db) : IReporteRepository
{
    private static readonly IReadOnlyDictionary<string, ReporteDefinition> Catalogo = ReporteCatalogo.Crear();

    public async Task<Result<ReporteDto>> ConsultarAsync(ReporteQuery query, CancellationToken ct)
    {
        if (!Catalogo.TryGetValue(query.Codigo, out var definition))
            return Result<ReporteDto>.Failure(new Error("reporte.no_encontrado", "Reporte no disponible.", ErrorType.NotFound));
        var filtros = Parametros(query.Filtros);
        // No ignorar filtros incompatibles: evitar resultados aparentemente filtrados.
        foreach (var filtro in filtros.Keys)
            if (definition.Secciones.Any(s => !s.Filtros.Contains(filtro, StringComparer.Ordinal)))
                return Result<ReporteDto>.Failure(new Error("reporte.filtro_incompatible", $"El reporte {query.Codigo} no admite el filtro {filtro}.", ErrorType.Validation));

        // Filas, cantidad y totales corresponden a una sola instantánea de PostgreSQL.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var secciones = new List<SeccionReporteDto>();
        foreach (var section in definition.Secciones)
        {
            string where = filtros.Count == 0 ? "" : " WHERE " + string.Join(" AND ", filtros.Keys.Select(k => k switch
            {
                "desde" => "fecha >= @desde",
                "hasta" => "fecha < @hasta",
                "estado" => $"{section.EstadoColumna} = @estado",
                _ => $"{k} = @{k}"
            }));
            string origen = $" FROM bi.{section.Vista}{where}";
            string dims = string.Join(", ", section.Dimensiones.Select(d => d.Columna));
            string metricas = string.Join(", ", section.Metricas.Select(m => $"{m.Expresion} AS \"{m.Campo}\""));
            string select = string.Join(", ", section.Dimensiones.Select(d => $"{d.Columna} AS \"{d.Campo}\""));
            string agrupacion = dims.Length == 0 ? "" : $" GROUP BY {dims}";
            string baseSql = $"SELECT {select}{(select.Length == 0 ? "" : ", ")}{metricas}{origen}{agrupacion}";
            long cantidad;
            await using (var cmd = Command($"SELECT count(*) FROM ({baseSql}) AS grupos", filtros))
                cantidad = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
            string orden = section.Orden ?? string.Join(", ", section.Dimensiones.Select(d => $"\"{d.Campo}\" NULLS FIRST"));
            var pageParams = new Dictionary<string, object>(filtros)
            {
                ["limite"] = query.Filtros.PageSize,
                ["offset"] = (query.Filtros.Page - 1) * query.Filtros.PageSize
            };
            var filas = await ReadAsync($"{baseSql}{(orden.Length == 0 ? "" : $" ORDER BY {orden}")} LIMIT @limite OFFSET @offset", pageParams, ct);
            bool moneda = section.Dimensiones.Any(d => d.Columna == "moneda");
            var totales = await ReadAsync($"SELECT {(moneda ? "moneda, " : "")}{metricas}{origen}{(moneda ? " GROUP BY moneda ORDER BY moneda" : "")}", filtros, ct);
            var columnas = section.Dimensiones.Select(d => new ColumnaReporteDto(d.Campo, d.Titulo, d.Tipo))
                .Concat(section.Metricas.Select(m => new ColumnaReporteDto(m.Campo, m.Titulo, m.Tipo))).ToArray();
            secciones.Add(new(section.Codigo, columnas, section.Dimensiones.Select(d => d.Campo).ToArray(), filas,
                totales, cantidad, query.Filtros.Page, query.Filtros.PageSize));
        }
        await tx.CommitAsync(ct);
        return Result<ReporteDto>.Success(new(query.Codigo, definition.Titulo, definition.Nota, query.Filtros, secciones));
    }

    private DbCommand Command(string sql, IReadOnlyDictionary<string, object> parameters)
    {
        var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;
        foreach (var (name, value) in parameters)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            cmd.Parameters.Add(parameter);
        }
        return cmd;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadAsync(string sql, IReadOnlyDictionary<string, object> parameters, CancellationToken ct)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var cmd = Command(sql, parameters);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    private static Dictionary<string, object> Parametros(ReporteFiltros f)
    {
        var result = new Dictionary<string, object>();
        void Add(string key, object? value) { if (value is not null) result.Add(key, value); }
        Add("desde", f.Desde); Add("hasta", f.Hasta); Add("evento_id", f.EventoId);
        Add("categoria_id", f.CategoriaId); Add("recinto_id", f.RecintoId); Add("dueno_id", f.DuenoId);
        Add("estado", f.Estado); Add("moneda", f.Moneda); Add("metodo", f.Metodo);
        return result;
    }
}
