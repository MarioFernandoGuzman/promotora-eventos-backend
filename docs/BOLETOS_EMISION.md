# Emisión de boletos y firma — F5-05

Estado: componente interno implementado, registrado y probado. No se añadió un endpoint de emisión ni se conectó el cobro/emisión a POST /api/v1/compras. Esa integración espera F7-02/F7-03 para confirmar pago, ledger, saldo y COMPLETADA juntos. Swagger tiene 59 operaciones; POST sigue reservando PENDIENTE. Las consultas de boletos/QR y validación en puerta F5-06/F5-07 están implementadas; ver [BOLETOS_VALIDACION.md](BOLETOS_VALIDACION.md).

## Módulos

| Capa | Implementación |
|---|---|
| Domain/Ticketing | Boleto y DatosAsistente: emisión VALIDO, UTC, datos obligatorios en carreras y JSON válido |
| Domain/Finance | RespaldoCobro: componentes congelados y cantidad exacta de movimientos no cero |
| Application/Ticketing | IEmisionBoletosPort, repositorio, ITicketTokenService y EmisionBoletosService |
| Infrastructure/Ticketing | PostgreSQL, bloqueo de compra, mapeo manual y HmacTicketTokenService |
| API/Worker | DI con configuración externa; sin nuevos controllers ni consumidores |

EmitirAsync administra su transacción. EnTransaccionAsync exige una transacción existente y permite al futuro finalizador de F7 incorporar emisión sin abrir otra transacción. El llamador debe revertir ante Result fallido o excepción. Estos son puertos internos de backend; no sustituyen la autorización por propiedad de las consultas HTTP futuras.

Se bloquea compras FOR UPDATE antes de leer boletos. Compra debe estar COMPLETADA y tener detalles válidos. Si ya hay exactamente un boleto por unidad, se devuelven los existentes con sus firmas/datos originales, aunque estén USADO/ANULADO; no se reactivan ni se vuelven a emitir. Una emisión parcial existente devuelve conflicto y requiere conciliación, sin completar silenciosamente una situación anómala.

Para una emisión nueva con total positivo: exactamente un pago APROBADO con referencia public_id y monto total; VENTA, COMISION e IMPUESTO en las cuentas correctas/moneda/dueño; componentes coincidentes con los montos congelados; una fila por componente no cero, sin movimientos negativos u otros tipos; saldos de las cuentas involucradas iguales a SUM de todos sus movimientos. Una gratuita exige cero movimientos financieros. No se recalculan tasas. Este bloque consulta esos registros: todavía no los produce.

Se genera una fila por unidad de detalle_compra.cantidad. Todas las filas y sus auditorías CREATE se confirman juntas. Un error de datos/firma o fallo de auditoría revierte la emisión. La auditoría identifica el boleto por su ID, con usuario null para el proceso interno; no registra QR ni datos personales.

## Firma y carreras

Formato: v1 + public_id del boleto (UUID aleatorio) + HMAC-SHA256 en Base64Url sin padding; longitud 79, dentro de VARCHAR(100). ITicketTokenService valida formato canónico, ID, versión y firma en tiempo constante. Verificar firma no valida el estado de entrada: F5-07 también debe realizar el UPDATE condicional VALIDO → USADO. El frontend representará el token como imagen QR; no se añadió una librería de dibujo al backend.

Boletos:SigningKey es una clave Base64 externa de al menos 32 bytes, independiente de JWT. No existe clave predeterminada ni fallback a Jwt:SigningKey. Sin clave, la API actual puede arrancar, pero la emisión falla con boleto.configuracion. Configurar Base64 inválido o clave demasiado corta impide registrar el módulo. No rotar esta clave sin una estrategia explícita: los boletos emitidos deben seguir siendo validables. Nunca entregar esta clave al frontend ni registrar tokens QR completos.

Boletos:CategoriaCarreraId indica el ID estable de la categoría Carrera ya existente. Se comprueba que exista. Usar el ID evita que renombrar la categoría elimine las exigencias del corredor. Si falta/es inválido, la emisión falla cerrada con boleto.categoria. El valor se configura también en Worker; no se crea columna ni categoría adicional.

AsistentesDetalle usa el ID interno del detalle y una lista ordenada, exactamente un DatosAsistente por unidad. Para carreras son obligatorios nombre_asistente y documento_identidad; espacios vacíos se rechazan. En otros eventos los datos son opcionales, pero una lista enviada debe estar completa. datos_extra es JSON opcional, p. ej. talla/contacto de emergencia; no se impuso una lista de claves nueva. El contrato HTTP para capturar esos datos se conectará con la finalización, sin aceptar datos incompletos ni guardarlos en una compra ajena.

## Configuración local, comandos separados

Desde la raíz, con conexión/JWT/bootstrap ya preparados según [DESARROLLO_LOCAL.md](DESARROLLO_LOCAL.md). Este bloque no cambia tu usuario ni contraseña.

En Swagger, consultar GET /api/v1/categorias con sesión y copiar el ID de la categoría Carrera existente. Leerlo localmente:

```powershell
$categoriaCarreraId = [int](Read-Host 'ID real de la categoría Carrera')
```

```powershell
dotnet user-secrets set 'Boletos:CategoriaCarreraId' "$categoriaCarreraId" --project src/Promotora.WebApi
```

```powershell
dotnet user-secrets set 'Boletos:CategoriaCarreraId' "$categoriaCarreraId" --project src/Promotora.Worker
```

Generar una clave compatible con Windows PowerShell 5.1 y PowerShell 7, sin imprimirla:

```powershell
$qrBytes = New-Object byte[] 32
```

```powershell
$qrRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
```

```powershell
$qrRng.GetBytes($qrBytes)
```

```powershell
$qrRng.Dispose()
```

```powershell
$qrKey = [Convert]::ToBase64String($qrBytes)
```

Guardar la misma clave externa en ambos hosts; no copiarla a archivos del repositorio:

```powershell
dotnet user-secrets set 'Boletos:SigningKey' $qrKey --project src/Promotora.WebApi
```

```powershell
dotnet user-secrets set 'Boletos:SigningKey' $qrKey --project src/Promotora.Worker
```

```powershell
Remove-Variable qrKey, qrBytes, qrRng
```

Si ya tienes una clave QR configurada, conservarla en vez de generar otra. Variables de entorno equivalentes: Boletos__SigningKey y Boletos__CategoriaCarreraId. Worker carga user-secrets en Development; si tu terminal no usa ese entorno, fijarlo antes de ejecutar:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
```

## Cómo comprobar este bloque

Con Docker operativo, ejecutar por separado:

```powershell
dotnet build --no-restore -warnaserror -c Release
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*HmacTicketTokenTests'
```

```powershell
dotnet test --project tests/Promotora.Infrastructure.Tests --no-build -c Release --filter-class '*EmisionBoletosTests'
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

Las pruebas usan claves aleatorias solo en memoria y PostgreSQL temporal con Flyway/rol restringido. Preparan compras completadas y respaldo financiero dentro de fixtures para verificar el componente; no escriben en la base de Compose. Nunca modificar manualmente estado/pagos/ledger locales para forzar una emisión.

En http://localhost:5080/swagger funcionan login, catálogo, reserva/consulta/cancelación de compra y consulta/validación de boletos existentes. No esperes pago ni QR en la respuesta de reserva todavía. Para verificar firma/emisión, ejecutar las pruebas anteriores; para el pago simulado, seguir [PAGOS_SIMULADOS.md](PAGOS_SIMULADOS.md).
