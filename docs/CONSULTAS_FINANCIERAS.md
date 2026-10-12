# Consultas financieras — F7-06

## Contratos y permisos

Las tres rutas requieren sesión completa de ADMINISTRADOR o DUENO_RECINTO. El handler comprueba en PostgreSQL que el usuario esté activo y no tenga cambio de contraseña pendiente. Un administrador consulta todas las cuentas, incluidas PLATAFORMA e IMPUESTOS. Un dueño consulta únicamente las cuentas DUENO de su perfil, tanto ACTIVA como CERRADA. CLIENTE recibe 403; una cuenta ajena o inexistente devuelve 404 sin revelar cuál de ambos casos ocurrió.

### Listado de cuentas

`GET /api/v1/cuentas?duenoId=&moneda=&tipo=&estado=&page=1&pageSize=20`

Filtros opcionales: duenoId positivo (ID del perfil duenos_recinto, no usuario_id), moneda de tres letras mayúsculas, tipo DUENO/PLATAFORMA/IMPUESTOS y estado ACTIVA/CERRADA. Para un dueño, los filtros siempre se intersectan con sus cuentas: filtrar otro perfil o un tipo del sistema devuelve una lista vacía.

Orden por ID ascendente. page entre 1 y 100000, pageSize entre 1 y 100; valores por defecto 1 y 20. Una página fuera de resultados devuelve items vacío y conserva total. El formato sigue PaginaDto del proyecto:

```json
{"items":[{"id":7,"tipo":"DUENO","duenoId":3,"moneda":"GTQ","saldo":200.00,"estado":"ACTIVA","fechaCierre":null}],"page":1,"pageSize":20,"total":1}
```

### Estado de cuenta

`GET /api/v1/cuentas/{id}` devuelve CuentaDto con ID, tipo, dueño, moneda, saldo, estado y fechaCierre UTC. El saldo representa todos los movimientos de la cuenta; los filtros del extracto no cambian ese saldo. No se recalculan impuestos ni comisiones.

### Extracto de movimientos

`GET /api/v1/cuentas/{id}/movimientos?tipo=&compraId=&desde=&hasta=&page=1&pageSize=20`

Filtros opcionales: tipo VENTA/COMISION/IMPUESTO/LIQUIDACION/REEMBOLSO, compraId positivo y fechas UTC con Z o +00:00. desde es inclusivo y hasta exclusivo; si se envían ambos, desde debe ser anterior a hasta. Ejemplo para un año: desde=2026-01-01T00:00:00Z y hasta=2027-01-01T00:00:00Z. En una URL manual codificar el signo + de +00:00 como %2B, o usar Z.

Orden por fecha descendente y, a igualdad de fecha, ID descendente. Paginación y límites iguales al listado de cuentas. monto mantiene el signo contable: positivo ingresa, negativo retira. Las liquidaciones tienen compraId null. No se exponen la descripción interna ni referencias de idempotencia.

```json
{"items":[{"id":42,"cuentaId":7,"compraId":null,"tipo":"LIQUIDACION","monto":-100.00,"fecha":"2026-10-11T18:00:00Z"}],"page":1,"pageSize":20,"total":2}
```

| HTTP | Caso |
|---|---|
| 200 | Consulta válida, incluida una lista vacía |
| 400 | ID, filtro, fecha, intervalo o paginación inválidos |
| 401/403 | Sin sesión, rol no autorizado, usuario inhabilitado o cambio de contraseña pendiente |
| 404 | Cuenta ajena/inexistente en detalle o extracto |

Las respuestas no se cachean. Las consultas son lecturas operativas, no una exportación ni un snapshot congelado entre peticiones: movimientos nuevos pueden desplazar páginas. Para conciliación, comparar saldo y SUM del ledger dentro de una misma consulta PostgreSQL; no sumar una página de resultados y asumir que representa todo el saldo.

## Módulos

Application/Finance define queries, DTO, validadores, handlers, acceso compartido y el puerto ICuentaConsultaRepository. Infrastructure/Finance implementa las consultas EF sin tracking y aplica el ámbito de dueño que decidió el handler. WebApi amplía CuentasController; no contiene SQL, EF ni reglas de propiedad. Sin paquetes ni migraciones nuevas. No hay operaciones de escritura financieras ni auditoría de exportación: son consultas, no reportes exportados.

## Probar localmente

Mantener los secretos actuales. Preparación en [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md) y autenticación en [FRONTEND_SWAGGER_AUTENTICACION.md](FRONTEND_SWAGGER_AUTENTICACION.md). Ejecutar cada comando desde la raíz del repositorio:

```powershell
docker compose up -d
```

```powershell
docker compose run --rm flyway migrate
```

```powershell
dotnet run --project src/Promotora.WebApi
```

1. Abrir http://localhost:5080/swagger y autenticar como DUENO_RECINTO habilitado. Consultar GET cuentas: aparecen solo sus cuentas y ahora puede obtener el ID sin consultar SQL.
2. Consultar el detalle y extracto por ese ID. Una cuenta nueva tiene saldo cero y extracto vacío; una cerrada sigue siendo consultable.
3. Para generar movimientos, completar una compra de su evento siguiendo [COMPRAS_ASINCRONAS.md](COMPRAS_ASINCRONAS.md). La cuenta del dueño recibe VENTA positiva por el subtotal.
4. Como ADMINISTRADOR, registrar un retiro según [LIQUIDACIONES.md](LIQUIDACIONES.md); comprobar LIQUIDACION negativa y saldo reducido. El filtro compraId muestra los movimientos de compra, excluyendo liquidaciones.
5. Probar pageSize=1 y page=1/2; filtrar tipo y fechas UTC. Una fecha exacta incluida en desde debe aparecer; usada como hasta queda excluida.
6. Como administrador, filtrar cuentas PLATAFORMA/IMPUESTOS y consultar sus comisiones/impuestos. Como dueño, esas consultas por ID deben devolver 404; como cliente, las tres rutas deben devolver 403.
7. Enviar page=0, pageSize=101, tipo inválido o desde >= hasta: 400 ProblemDetails. Confirmar que consultar no cambia saldo, cupo ni movimientos.

No registrar tokens o contraseñas en archivos. No se define ninguna contraseña nueva para estas pruebas; usar cuentas ya creadas mediante el flujo de administración.

## Pruebas automáticas

Docker Desktop operativo. No necesitan API/Worker iniciados ni credenciales locales.

```powershell
dotnet build -c Release -warnaserror
```

```powershell
dotnet test --project tests/Promotora.Api.Tests --no-build -c Release --filter FullyQualifiedName~ConsultasFinanzasApiTests --report-trx
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --no-restore --verify-no-changes
```

Las pruebas usan PostgreSQL temporal migrado desde cero y ventas completadas con los adaptadores financieros reales. Cubren propiedad, cuentas cerradas/sistema, filtros, paginación, signo e intervalos exactos, token antiguo, errores y Swagger. La consulta no expone descripcion del ledger.

Verificación F7-06: **414 pruebas Release aprobadas, 0 fallidas, 0 omitidas**; build sin warnings/errores y formato limpio. Cinco casos API nuevos. Swagger incluye 65 operaciones.
