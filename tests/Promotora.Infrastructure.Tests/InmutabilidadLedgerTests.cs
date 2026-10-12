using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Promotora.Infrastructure.Tests;

public sealed class InmutabilidadLedgerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("UPDATE", false)]
    [InlineData("DELETE", false)]
    [InlineData("UPDATE", true)]
    [InlineData("DELETE", true)]
    public async Task PermisosYTriggerProtegenUnaFilaRealSinCambiarSaldo(string operacion, bool propietario)
    {
        await using var db = postgres.CreateContext();
        var venta = await ReembolsoFixture.VentaAsync(db, postgres.ConnectionString, RandomNumberGenerator.GetBytes(32), false, Ct);
        var original = await db.MovimientosFinancieros.AsNoTracking().SingleAsync(m => m.CompraId == venta.Id && m.Tipo == "VENTA", Ct);
        decimal saldo = await db.CuentasFinancieras.Where(c => c.Id == original.CuentaId).Select(c => c.Saldo).SingleAsync(Ct);
        await using var conexion = propietario ? postgres.CreateAdminConnection() : new NpgsqlConnection(postgres.ConnectionString);
        await conexion.OpenAsync(Ct); await using var tx = await conexion.BeginTransactionAsync(Ct);
        if (propietario)
        {
            // Flyway es dueño de la tabla: tiene permisos DML, pero el trigger debe rechazar su mutación.
            await using var rol = new NpgsqlCommand("SET LOCAL ROLE flyway", conexion, tx); await rol.ExecuteNonQueryAsync(Ct);
        }
        await using (var privilegios = new NpgsqlCommand("SELECT has_table_privilege(current_user,'public.movimientos_financieros','SELECT'), has_table_privilege(current_user,'public.movimientos_financieros','INSERT'), has_table_privilege(current_user,'public.movimientos_financieros','UPDATE'), has_table_privilege(current_user,'public.movimientos_financieros','DELETE')", conexion, tx))
        await using (var lectura = await privilegios.ExecuteReaderAsync(Ct))
        {
            Assert.True(await lectura.ReadAsync(Ct)); Assert.True(lectura.GetBoolean(0)); Assert.True(lectura.GetBoolean(1));
            Assert.Equal(propietario, lectura.GetBoolean(2)); Assert.Equal(propietario, lectura.GetBoolean(3));
        }
        string sql = operacion switch
        {
            "UPDATE" => "UPDATE public.movimientos_financieros SET monto=monto+1 WHERE id=@id",
            "DELETE" => "DELETE FROM public.movimientos_financieros WHERE id=@id",
            _ => throw new ArgumentOutOfRangeException(nameof(operacion))
        };
        await using var comando = new NpgsqlCommand(sql, conexion, tx); comando.Parameters.AddWithValue("id", original.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(Ct));
        Assert.Equal(propietario ? PostgresErrorCodes.CheckViolation : PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        if (propietario) Assert.Contains("solo admite INSERT", error.MessageText);
        await tx.RollbackAsync(Ct);
        var despues = await db.MovimientosFinancieros.AsNoTracking().SingleAsync(m => m.Id == original.Id, Ct);
        Assert.Equal(original.CuentaId, despues.CuentaId); Assert.Equal(original.CompraId, despues.CompraId);
        Assert.Equal(original.Tipo, despues.Tipo); Assert.Equal(original.Monto, despues.Monto);
        Assert.Equal(original.Fecha, despues.Fecha); Assert.Equal(original.Descripcion, despues.Descripcion);
        Assert.Equal(3, await db.MovimientosFinancieros.CountAsync(m => m.CompraId == venta.Id, Ct));
        Assert.Equal(saldo, await db.CuentasFinancieras.Where(c => c.Id == original.CuentaId).Select(c => c.Saldo).SingleAsync(Ct));
        Assert.Equal(saldo, await db.MovimientosFinancieros.Where(m => m.CuentaId == original.CuentaId).SumAsync(m => m.Monto, Ct));
    }
}
