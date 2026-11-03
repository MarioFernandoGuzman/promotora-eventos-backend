using Microsoft.EntityFrameworkCore;
using Npgsql;
using Promotora.Application.AI.Consultas;
using Promotora.Infrastructure.AI.Persistence;

namespace Promotora.Infrastructure.Tests;

public sealed class LogIaPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task GuardaMetadatosYCitasSinCredencialesYElLogEsInmutable()
    {
        var ct = TestContext.Current.CancellationToken; await using var db = postgres.CreateContext(); Guid session = Guid.NewGuid();
        Assert.Equal("[Contenido omitido por seguridad]", ConsultaPrivacidad.Redactar("mi api_key: clave-de-prueba"));
        await new EfConsultaIaLogPort(db).GuardarAsync(new(session, "Reglamento público", "Cita literal", [new("rag:7", "FAQ", 2, 7, 1, null, "Cita literal")], "OK", "fake", 3, 4, 5, DateTime.UtcNow), ct);
        var log = await db.LogsAgenteIa.AsNoTracking().SingleAsync(l => l.SessionId == session, ct);
        Assert.Null(log.UsuarioId); Assert.Equal(7, log.FuentesCitadas!.RootElement[0].GetProperty("chunkId").GetInt64()); Assert.Equal(3, log.TokensEntrada);
        await Assert.ThrowsAsync<PostgresException>(() => db.LogsAgenteIa.Where(l => l.Id == log.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.Estado, "ERROR"), ct));
    }
}
