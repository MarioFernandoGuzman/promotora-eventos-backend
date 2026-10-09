# Dependencias de la solución

Versiones fijadas centralmente en `Directory.Packages.props`. Domain no tiene paquetes externos.

| Paquete | Versión | Uso | Licencia / fuente oficial |
|---|---|---|---|
| Microsoft.Extensions.Hosting | 10.0.10 | Host del Worker | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| xunit.v3 | 4.0.1 | Pruebas en los cinco proyectos | [Apache-2.0](https://github.com/xunit/xunit/blob/main/LICENSE) |
| Microsoft.Testing.Extensions.TrxReport | 2.4.0 | Resultados TRX con Microsoft.Testing.Platform | [MIT](https://github.com/microsoft/testfx/blob/main/LICENSE) |
| Microsoft.EntityFrameworkCore | 10.0.10 | Persistencia EF, sin generar migraciones | [MIT](https://github.com/dotnet/efcore/blob/main/LICENSE.txt) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Proveedor PostgreSQL para EF 10 | [PostgreSQL](https://github.com/npgsql/efcore.pg/blob/main/LICENSE) |
| EFCore.NamingConventions | 10.0.1 | Convención snake_case | [Apache-2.0](https://www.nuget.org/packages/EFCore.NamingConventions/10.0.1) |
| Pgvector.EntityFrameworkCore | 0.3.0 | Vector 1536 y distancia coseno (EF 9/10) | [MIT](https://www.nuget.org/packages/Pgvector.EntityFrameworkCore/0.3.0) |
| Testcontainers.PostgreSql | 4.16.0 | PostgreSQL+pgvector temporal en pruebas | [MIT](https://github.com/testcontainers/testcontainers-dotnet/blob/develop/LICENSE) |
| FluentValidation | 12.1.1 | Validadores async y decoradores propios en Application | [Apache-2.0](https://www.nuget.org/packages/FluentValidation/12.1.1) |

Se usa xUnit v3 porque v2 está deprecado para desarrollo nuevo. El runner Microsoft.Testing.Platform está configurado en `global.json`; los proyectos de pruebas son ejecutables, como requiere v3. Se generan TRX con `dotnet test --report-trx` (el argumento VSTest `--logger` ya no aplica). F2-07 adapta el CI. En F2-01 aún no hay pruebas: el runner devuelve código 8 por cero pruebas; las pruebas reales se añaden con las tareas siguientes.
Documentación: [inicio de xUnit v3](https://xunit.net/docs/getting-started/v3/getting-started).
