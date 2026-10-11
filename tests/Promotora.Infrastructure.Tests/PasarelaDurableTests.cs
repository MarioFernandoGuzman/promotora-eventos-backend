using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Application.Finance.Ports;
using Promotora.Infrastructure.Finance;
using Promotora.Infrastructure.Shared;

namespace Promotora.Infrastructure.Tests;

public sealed class PasarelaDurableTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SystemClock Clock = new(TimeProvider.System);

    [Theory]
    [InlineData(0, "APROBADO")]
    [InlineData(1, "RECHAZADO")]
    public async Task VeinteInstanciasCompartenPrimerResultadoInclusoConOtraTasaTrasReinicio(int tasa, string estado)
    {
        var solicitud = new SolicitudPago(Guid.NewGuid(), 123.20m, "GTQ");
        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => new PasarelaSimuladaDurable(postgres.ConnectionString, new(tasa), Clock).CobrarAsync(solicitud, Ct)));
        Assert.All(resultados, r => { Assert.True(r.IsSuccess); Assert.Equal(estado, r.Value.Estado); Assert.Equal(resultados[0].Value, r.Value); });
        var nuevo = new PasarelaSimuladaDurable(postgres.ConnectionString, new(1 - tasa), Clock);
        Assert.Equal(resultados[0].Value, await nuevo.ConsultarAsync(solicitud.Referencia, Ct));
        Assert.Equal(resultados[0].Value, (await nuevo.CobrarAsync(solicitud, Ct)).Value);
        Assert.True((await nuevo.CobrarAsync(solicitud with { Monto = 124m }, Ct)).IsFailure);
        await using var db = postgres.CreateContext(); Assert.Equal(1, await db.PagosSimulados.CountAsync(p => p.Referencia == solicitud.Referencia, Ct));
    }

    [Fact]
    public async Task DiarioSobreviveRollbackDeLaConexionDeNegocioYSoloPermiteInsert()
    {
        var referencia = Guid.NewGuid(); await using var db = postgres.CreateContext();
        await using (var tx = await db.Database.BeginTransactionAsync(Ct))
        {
            Assert.True((await new PasarelaSimuladaDurable(postgres.ConnectionString, new(), Clock).CobrarAsync(new(referencia, 0m, "GTQ"), Ct)).IsSuccess);
            await tx.RollbackAsync(Ct);
        }
        Assert.Equal("APROBADO", (await new PasarelaSimuladaDurable(postgres.ConnectionString, new(1m), Clock).ConsultarAsync(referencia, Ct))!.Estado);
        foreach (string sql in new[] { "UPDATE pagos_simulados SET monto=1 WHERE referencia=@id", "DELETE FROM pagos_simulados WHERE referencia=@id" })
        {
            await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue("id", referencia);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct)); Assert.Equal("42501", error.SqlState);
        }
        Assert.Null(await new PasarelaSimuladaDurable(postgres.ConnectionString, new(), Clock).ConsultarAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task CancelacionYDatosInvalidosNoCreanResultado()
    {
        var gateway = new PasarelaSimuladaDurable(postgres.ConnectionString, new(), Clock);
        var solicitud = new SolicitudPago(Guid.NewGuid(), -1m, "GTQ");
        Assert.True((await gateway.CobrarAsync(solicitud, Ct)).IsFailure);
        Assert.Null(await gateway.ConsultarAsync(solicitud.Referencia, Ct));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.CobrarAsync(solicitud with { Monto = 1m }, cancel.Token));
    }
}
