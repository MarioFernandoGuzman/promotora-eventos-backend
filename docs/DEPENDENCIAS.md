# Dependencias de la solución

Versiones fijadas centralmente en `Directory.Packages.props`. Domain no tiene paquetes externos.

| Paquete | Versión | Uso | Licencia / fuente oficial |
|---|---|---|---|
| Microsoft.Extensions.Hosting | 10.0.10 | Host del Worker | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| xunit.v3 | 4.0.1 | Pruebas en los cinco proyectos | [Apache-2.0](https://github.com/xunit/xunit/blob/main/LICENSE) |
| Microsoft.Testing.Extensions.TrxReport | 2.4.0 | Resultados TRX con Microsoft.Testing.Platform | [MIT](https://github.com/microsoft/testfx/blob/main/LICENSE) |

Se usa xUnit v3 porque v2 está deprecado para desarrollo nuevo. El runner Microsoft.Testing.Platform está configurado en `global.json`; los proyectos de pruebas son ejecutables, como requiere v3. Se generan TRX con `dotnet test --report-trx` (el argumento VSTest `--logger` ya no aplica). F2-07 adapta el CI. En F2-01 aún no hay pruebas: el runner devuelve código 8 por cero pruebas; las pruebas reales se añaden con las tareas siguientes.
Documentación: [inicio de xUnit v3](https://xunit.net/docs/getting-started/v3/getting-started).
