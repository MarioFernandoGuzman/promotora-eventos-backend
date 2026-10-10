# Consultas y validación de entrada — F5-06/F5-07

## Módulos implementados

Application/Ticketing define queries/handlers de compra propia, listas paginadas, cancelación y validación de boleto; FluentValidation verifica UUID y paginación/longitud. Infrastructure/Ticketing implementa consultas EF y actualizaciones condicionales en PostgreSQL. WebApi expone los contratos y Swagger; Domain mantiene las reglas existentes. No se añadieron paquetes ni migraciones.

GET /api/v1/compras/{publicId}, GET /api/v1/compras y GET /api/v1/boletos requieren CLIENTE activo con sesión completa. La identidad procede del JWT y se revalida en los handlers. Los datos ajenos no aparecen; una compra ajena se trata igual que una inexistente (404). Listas: page=1, pageSize=20 por defecto, máximo 100, con {items,page,pageSize,total}; se ordenan por fecha e ID descendentes. Consultas con Cache-Control: no-store.

POST /api/v1/compras/{publicId}/cancelar no recibe body. Solo PENDIENTE pasa a CANCELADA; el ganador devuelve cupo y audita UPDATE en la misma transacción. Repetir CANCELADA devuelve 200 sin nuevos efectos. PROCESANDO y otros estados devuelven 409. Fallar la auditoría revierte estado y cupo. El handler de expiración F5-08 está disponible; la ejecución automática depende de F6-07, ver [EXPIRACION_COMPRAS.md](EXPIRACION_COMPRAS.md).

## Contrato para la puerta

```http
POST /api/v1/boletos/validar
Authorization: Bearer <accessToken de administrador o dueño>
Content-Type: application/json
```

```json
{"codigoQr":"<token completo recibido del boleto emitido>"}
```

El placeholder se sustituye por el texto exacto del QR. No agregar Bearer al codigoQr ni alterar su contenido. El frontend escanea el QR y envía su texto; la clave Boletos:SigningKey permanece solo en backend. Su configuración está en [BOLETOS_EMISION.md](BOLETOS_EMISION.md). API/Worker deben usar la misma clave externa que emitió los boletos.

Requiere ADMINISTRADOR o DUENO_RECINTO activo, con sesión completa. El handler comprueba rol/estado incluso si se llama directamente. Un dueño solo puede validar el recinto del evento correspondiente; otro recinto responde 404. El administrador puede validar cualquier recinto.

Se valida HMAC y formato canónico antes de buscar el boleto. Se bloquean recinto, evento y compra para estabilizar propiedad/estado; la compra debe estar COMPLETADA. El UPDATE de boletos exige codigo_qr coincidente y estado VALIDO: solo una fila ganadora pasa a USADO y registra fecha_uso UTC y validado_por. La auditoría VALIDATE comparte transacción. La respuesta y el log de auditoría no incluyen QR ni datos del asistente. No cambia cupo ni ledger. No se impuso una ventana de acceso por fecha, pues el plan no define esa regla.

Respuesta 200:

```json
{"publicId":"<UUID del boleto>","estado":"USADO","fechaUso":"<fecha UTC>"}
```

| HTTP | Causa |
|---|---|
| 400 | Body inválido, token ausente o longitud distinta de 79 |
| 401 | JWT ausente/inválido o sesión revocada/usuario inactivo |
| 403 | CLIENTE u otro actor sin permisos/sesión completa |
| 404 | Token firmado sin boleto coincidente o boleto de otro recinto |
| 409 | USADO, ANULADO o compra sin estado COMPLETADA; no modifica el boleto |
| 422 | Firma/formato de token inválido, o falta una clave de firma utilizable |

Repetir un escaneo responde 409: la puerta no debe admitir otra entrada. Si hubo un corte de red después del primer 200, el reintento puede recibir 409 porque el boleto ya se consumió. La firma por sí sola no prueba que un boleto esté vigente; siempre consultar este endpoint. Evitar logs, analítica o persistencia de tokens completos en frontend.

## Prueba local en Swagger

Usar la configuración y credenciales externas ya existentes de [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md); este bloque no crea ni cambia usuarios/contraseñas. Con Docker funcionando, desde la raíz, ejecutar los comandos separados:

```powershell
docker compose up -d
```

```powershell
docker compose run --rm flyway migrate
```

```powershell
dotnet restore
```

```powershell
dotnet build --no-restore -warnaserror -c Release
```

```powershell
dotnet test --no-build -c Release --report-trx
```

```powershell
dotnet format --verify-no-changes --no-restore
```

```powershell
dotnet run --project src/Promotora.WebApi
```

1. Abrir http://localhost:5080/swagger. Login como CLIENTE y pegar accessToken en Authorize, sin prefijo Bearer.
2. Preparar una compra siguiendo [COMPRAS_API.md](COMPRAS_API.md); copiar su publicId. GET /compras/{publicId} devuelve PENDIENTE y los mismos montos congelados. GET /compras lista solo las propias.
3. POST /compras/{publicId}/cancelar devuelve CANCELADA. Repetir también devuelve 200; el cupo del catálogo aumenta una sola vez. Probar esa compra con otro CLIENTE devuelve 404. Cambiar de sesión requiere reemplazar el token en Authorize.
4. GET /boletos devuelve solo los emitidos propios. Puede estar vacío: POST /compras aún no cobra/emite por HTTP; la integración financiera espera F7-02/F7-03. No forzar estados/pagos/boletos con SQL en la base local.
5. Cuando existan boletos emitidos por el flujo completo, copiar codigoQr, iniciar sesión como ADMINISTRADOR o dueño del recinto y llamar POST /boletos/validar. Primer escaneo 200; repetido 409; dueño ajeno 404; CLIENTE 403. Swagger documenta el body y todos estos errores.

Para comprobar hoy el caso exitoso y las carreras, las pruebas crean fixtures gratuitas con boletos firmados en PostgreSQL temporal. No escriben en Compose ni requieren claves reales:

```powershell
dotnet test --project tests/Promotora.Api.Tests --no-build -c Release --filter-class '*CompraConsultasApiTests'
```

```powershell
dotnet test --project tests/Promotora.Api.Tests --no-build -c Release --filter-class '*BoletoValidacionApiTests'
```

Casos: veinte cancelaciones con cupo/auditoría únicos; veinte escaneos con dos administradores, un 200 y diecinueve 409; rollback de cancelación y validación; propiedad; roles/usuario inactivo; firma alterada; QR sin clave; boletos USADO/ANULADO; compra no completada; paginación; contratos y Swagger. Las claves y contraseñas de prueba se generan solo en memoria.
