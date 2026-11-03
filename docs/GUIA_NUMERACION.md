# Probar localidades, mesas y sillas numeradas

## Alcance

Backend .NET: distribución por evento/localidad, modalidades COMPLETA y POR_SILLA, reserva mixta indivisible, QR por persona y ubicación histórica. La mesa cuesta la suma de sus sillas; todas comparten tipo de entrada/precio. No hay pasarela externa ni devolución bancaria real.

El frontend Vue existente es exclusivamente una herramienta local: no forma parte del repo ni del PR. Su selector de posiciones, envío de asientoIds y visualización de ubicación se probaron localmente. No se elige D-09 ni se marca Fase 11 como implementada. `.git/info/exclude` excluye /frontend/ y /scripts/Start-Frontend.ps1; no usar git add -f ni añadir dist/node_modules. Una instalación nueva del backend se prueba con Swagger sin depender de esa herramienta.

## Arranque local

Abre Docker Desktop y PowerShell en la raíz del repo. El .env privado existente proporciona configuración; no copies credenciales a estos documentos.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Infra
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1 -Servicio Api
```

Swagger: http://localhost:5080/swagger. Tras actualizar código hay que reiniciar la API y cualquier Worker anterior: una instancia ya arrancada no carga automáticamente estos cambios. API y Worker deben usar el mismo código actualizado antes de vender posiciones. Las migraciones nuevas se aplican con Flyway usando la configuración externa existente, nunca editando V1–V12 ni reiniciando volúmenes.

Para completar compras se necesita Worker, RabbitMQ, outbox y consumidor habilitados. La configuración previa de pruebas de IA los mantenía deshabilitados: arrancar Worker sin habilitarlos deja las compras pendientes. Revisar conscientemente Outbox__Enabled, Compras__ConsumerEnabled, Expiracion__Enabled, Recuperacion__Enabled y Notificaciones__Enabled en el .env local antes de iniciar procesos; hay mensajes previos que podrían procesarse. No activar consumidores ni vaciar colas para probar solo un plano. Start-Local -Servicio Worker carga esa configuración y ejecuta el host existente. No requiere llamadas a IA.

## Configurar desde Swagger

1. Login con administrador habilitado o dueño del recinto y Authorize. Los clientes no configuran inventario. Otra propiedad responde 404.
2. Utiliza un evento de prueba con recinto/localidad activos y sin compras previas de esa zona. Crea sus tipos de entrada y conserva sus IDs. El máximo por compra debe admitir todas las personas de la mesa.
3. Cada localidad con distribución numerada deja de vender cupo general en ese evento al publicar. Usa otras localidades o un tipo sin localidad para entradas generales. No conviertas una localidad que ya tiene historial de compras.
4. Ejecuta PUT /api/v1/eventos/{eventoId}/localidades/{localidadId}/distribucion. Ejemplo de mesa de dos sillas: reemplaza 123 por el ID real de su tipo. Las etiquetas son únicas en la distribución, las claves de mesa conectan el payload y las coordenadas están entre 0 y 1.

```json
{
  "imagenPlanoUrl": null,
  "mesas": [
    { "id": null, "clave": "m1", "etiqueta": "Mesa 1", "modalidad": "COMPLETA", "tipoEntradaId": 123, "x": 0.5, "y": 0.5 }
  ],
  "asientos": [
    { "id": null, "mesaClave": "m1", "etiqueta": "M1-S1", "fila": null, "tipoEntradaId": 123, "x": 0.4, "y": 0.5, "bloqueado": false },
    { "id": null, "mesaClave": "m1", "etiqueta": "M1-S2", "fila": null, "tipoEntradaId": 123, "x": 0.6, "y": 0.5, "bloqueado": false }
  ]
}
```

Para vender por silla cambia modalidad a POR_SILLA antes de publicar. Para silla independiente usa mesaClave null y no la incluyas en una mesa. La primera versión conserva tarifa de una mesa existente que ya tiene sillas; para otra tarifa crea otra mesa. Al editar envía todos los IDs existentes; no omitir ni reutilizar IDs. Un borrador no borra filas; sillas excluidas se marcan bloqueado y cuentan en capacidad física.

5. Revisa GET .../distribucion?page=1&pageSize=100. La respuesta contiene dos listas paginadas (mesas y asientos), cada una con su total. Para inventario mayor continúa páginas hasta cargar ambos totales.
6. Ejecuta POST .../distribucion/publicar sin body. Publicar congela estructura/etiquetas y reconcilia cupos. Verifica el detalle público GET /api/v1/catalogo/eventos/{eventoId}: el tipo devuelve numerada=true.
7. GET .../asientos?page=1&pageSize=100 devuelve disponibilidad pública sin compradores. Puedes filtrar mesaId. La publicación solo se muestra cuando el evento y sus padres son públicos/activos. No interpreta posiciones ausentes de otra página como ocupadas.

## Comprar

Login como CLIENTE, Authorize y POST /api/v1/compras con header Idempotency-Key nuevo para esa intención. Copia los IDs reales de silla de la respuesta anterior, no sus etiquetas ni los valores ilustrativos siguientes.

```json
{
  "eventoId": 456,
  "items": [
    { "tipoEntradaId": 123, "cantidad": 2, "asientoIds": [1001, 1002], "asistentes": null }
  ]
}
```

COMPLETA exige todas las sillas, POR_SILLA permite una parte. Cantidad es número de personas. En una compra puedes añadir entradas generales del mismo evento sin asientoIds. Los asistentes, si se envían, corresponden al orden exacto de asientoIds; obligatorios en carreras. Los importes y pertenencia los verifica el servidor.

202 indica reserva pendiente, no boleto emitido. Consulta Location/GET compra hasta estado final. Si una posición fue tomada, 409 y ninguna reserva parcial. Repetir la misma clave propia devuelve la compra original; para cambiar selección crea otra intención con otra clave. Ante error de red conserva clave y body para reintento manual, no generes una compra nueva automáticamente.

Al completar se genera un QR por persona y GET /api/v1/boletos incluye ubicacion: asientoId, silla, fila, mesaId, mesa y localidadId. Para generales ubicacion es null. El correo y la respuesta de validación también incluyen ubicación. No loguear QR completos ni tokens.

## Operaciones y límites

- POST .../asientos/bloquear o desbloquear recibe un array JSON de IDs. Solo posiciones disponibles/bloqueadas propias; mesa COMPLETA se modifica íntegra. Actualiza cupo en la misma transacción.
- Cancelación solo desde PENDIENTE; expiración solo pendiente. Rechazo de pago confirmado libera. PROCESANDO incierto mantiene reserva hasta recuperación segura. Solo se libera la asignación de la compra ganadora.
- Reembolso total administrador: libera solo VALIDO -> ANULADO; USADO conserva silla/historial. Una mesa completa con una silla usada y otras reembolsadas no vuelve a estar íntegramente disponible, por lo que no se puede revender completa.
- No trasladar, renumerar ni reconfigurar estructura publicada. La configuración de otro evento tiene inventario independiente.
- Máximo 2000 sillas y 500 mesas por distribución; configuración 2 MiB; consultas default 20/máximo 100 por página. Sin WebSocket ni retención al hacer clic.

## Qué se conserva en el frontend local

El catálogo muestra el selector solo para numerada=true. Carga todas las páginas antes de habilitar selección, agrupa mesa COMPLETA, cuenta personas y adjunta asientoIds. Ante 409 limpia la selección numerada, renueva intención y recarga disponibilidad sin reenviar compra automáticamente. Mis boletos muestra mesa/silla junto al QR. La configuración administrativa inicial se hace con Swagger; no se crea un editor gráfico de distribución de producción en esta ampliación.

El frontend local no se distribuye con Git ni se instala en el backend. Las llamadas siguen siendo REST al API C#; Node/Vite se usan únicamente en esa herramienta cliente local.

## Actualizar conocimiento RAG existente

Los documentos de docs/rag no se reemplazan automáticamente en PostgreSQL. Sube nuevas versiones de REGLAMENTO.md y FAQ.md y ejecuta reindexar con el ID nuevo. La publicación retira la versión anterior de esa serie; no conservar dos documentos independientes con reglas contradictorias. La carga/indexación consume embeddings: no se ejecuta automáticamente en esta ampliación ni se llama a proveedores durante pruebas.

La primera versión toma bloqueos de las distribuciones de un evento en orden de ID, compartido por reserva, emisión y liberación. Favorece consistencia y evita órdenes contradictorios al combinar zonas; puede serializar operaciones del mismo evento. No se ha hecho un benchmark de 500/1000 solicitudes numeradas: esa evidencia histórica corresponde al flujo general. La prueba nueva cubre disputa concurrente por mesa y carreras de estado.
