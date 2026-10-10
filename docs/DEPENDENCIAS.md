# Dependencias de la solución

Versiones fijadas centralmente en `Directory.Packages.props`. Domain no tiene paquetes externos.

| Paquete | Versión | Uso | Licencia / fuente oficial |
|---|---|---|---|
| RabbitMQ.Client | 7.2.2 | Adaptador async v7, topología durable y publisher confirms detrás de IMessagePublisher | [Apache-2.0 (opción de licencia dual)](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/main/LICENSE), [NuGet](https://www.nuget.org/packages/RabbitMQ.Client/7.2.2) |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.10 | Validación JWT Bearer en WebApi | [MIT](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer/10.0.10) |
| System.IdentityModel.Tokens.Jwt | 8.19.2 | Firma HS256 detrás de ITokenService; alineado con la dependencia de JwtBearer | [MIT](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt/8.19.2) |
| Microsoft.Extensions.Identity.Core | 10.0.10 | PasswordHasher PBKDF2 con sal aleatoria; no instala tablas Identity ni reemplaza Flyway | [MIT](https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt) |
| Microsoft.Extensions.Hosting | 10.0.10 | Host del Worker | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| xunit.v3 | 4.0.1 | Pruebas en los cinco proyectos | [Apache-2.0](https://github.com/xunit/xunit/blob/main/LICENSE) |
| Microsoft.Testing.Extensions.TrxReport | 2.4.0 | Resultados TRX con Microsoft.Testing.Platform | [MIT](https://github.com/microsoft/testfx/blob/main/LICENSE) |
| Microsoft.EntityFrameworkCore | 10.0.10 | Persistencia EF, sin generar migraciones | [MIT](https://github.com/dotnet/efcore/blob/main/LICENSE.txt) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Proveedor PostgreSQL para EF 10 | [PostgreSQL](https://github.com/npgsql/efcore.pg/blob/main/LICENSE) |
| EFCore.NamingConventions | 10.0.1 | Convención snake_case | [Apache-2.0](https://www.nuget.org/packages/EFCore.NamingConventions/10.0.1) |
| Pgvector.EntityFrameworkCore | 0.3.0 | Vector 1536 y distancia coseno (EF 9/10) | [MIT](https://www.nuget.org/packages/Pgvector.EntityFrameworkCore/0.3.0) |
| SkiaSharp | 3.119.4 | Decodificar PNG/JPEG con límites y reexportar PNG desde píxeles en Infrastructure; evita guardar archivos originales no confiables | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md), [NuGet](https://www.nuget.org/packages/SkiaSharp/3.119.4) |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 3.119.4 | Binarios nativos compatibles con el CI/Linux, misma versión del decodificador; Windows/macOS vienen transitivamente con SkiaSharp | [MIT](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/3.119.4) |
| Testcontainers.PostgreSql | 4.16.0 | PostgreSQL+pgvector temporal en pruebas | [MIT](https://github.com/testcontainers/testcontainers-dotnet/blob/develop/LICENSE) |
| FluentValidation | 12.1.1 | Validadores async y decoradores propios en Application | [Apache-2.0](https://www.nuget.org/packages/FluentValidation/12.1.1) |
| Swashbuckle.AspNetCore | 10.3.0 | Documento OpenAPI v1 y Swagger UI | [MIT](https://www.nuget.org/packages/Swashbuckle.AspNetCore/10.3.0) |
| Serilog.AspNetCore | 10.0.0 | Logging estructurado de la API | [Apache-2.0](https://www.nuget.org/packages/Serilog.AspNetCore/10.0.0) |
| Serilog.Extensions.Hosting | 10.0.0 | Logging del Worker | [Apache-2.0](https://www.nuget.org/packages/Serilog.Extensions.Hosting/10.0.0) |
| Serilog.Sinks.Console | 6.1.1 | Salida de logs del Worker | [Apache-2.0](https://www.nuget.org/packages/Serilog.Sinks.Console/6.1.1) |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.10 | Contratos HTTP con WebApplicationFactory | [MIT](https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt) |
| Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions | 10.0.10 | Readiness del adaptador PostgreSQL | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| NetArchTest.Rules | 1.3.2 | Verificar dependencias y aislamiento de controllers | [MIT](https://github.com/BenMorris/NetArchTest/blob/master/LICENSE) |

Se usa xUnit v3 porque v2 está deprecado para desarrollo nuevo. El runner Microsoft.Testing.Platform está configurado en `global.json`; los proyectos de pruebas son ejecutables, como requiere v3. Se generan TRX con `dotnet test --report-trx` (el argumento VSTest `--logger` ya no aplica). F2-07 adapta el CI. La suite falla si un proyecto no ejecuta pruebas; no se permiten pruebas vacías para simular cobertura.
Documentación: [inicio de xUnit v3](https://xunit.net/docs/getting-started/v3/getting-started).
