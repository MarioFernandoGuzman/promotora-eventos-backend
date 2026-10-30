# Propuesta: localidades generales, sillas y mesas numeradas

Estado: diseño aprobado para implementación por el usuario el 29/10/2026. EXT-NUM-01 registra el diseño y EXT-NUM-02 implementa el esquema V12, mapeos y validación de selección. EXT-NUM-03 incorpora configuración, publicación, consulta y bloqueo. EXT-NUM-04 incorpora reserva mixta/asignaciones/idempotencia. EXT-NUM-05 integra emisión, liberación, ubicación y reembolso; venta lista para validación y despliegue conjunto API/Worker.

## 1. Decisiones confirmadas

- Un evento combina localidades generales y localidades numeradas.
- Se seleccionan sillas independientes o sillas dentro de una mesa.
- Cada mesa admite venta COMPLETA o POR_SILLA, elegida por evento; no ambas simultáneamente en la misma mesa.
- Comprar mesa completa exige todas sus sillas en una operación.
- Un boleto y QR por persona, con mesa/silla identificables.
- Precio de mesa = suma de precios de sus sillas. Comisión e IVA se calculan a nivel de compra según D-15.
- El máximo de boletos por compra cuenta personas: una mesa de seis necesita un límite de al menos seis.
- D-08 queda actualizada a venta mixta por aprobación del usuario. No se reescriben migraciones históricas.

## 2. Alcance propuesto para reducir complejidad

La distribución numerada se configura por evento/localidad y conserva etiquetas y coordenadas propias. El recinto mantiene sus localidades y plano base. Así dos eventos pueden usar distribuciones distintas sin compartir ocupación.

Primera versión: sin plantillas reutilizables de mesas/sillas del recinto; se podrán incorporar después mediante copia explícita a la distribución del evento. No es necesario añadir tablas de plantillas para vender posiciones ahora.

Cada silla pertenece a un único tipo de entrada del evento. Todas las sillas de una mesa comparten tipo y precio. Dos tarifas no pueden crear dos inventarios sobre la misma posición. Preventa y precio normal simultáneos sobre la misma silla quedan fuera de esta primera versión; requerirían un modelo separado de tarifas.

Una localidad numerada no admite simultáneamente entradas generales en ese evento. Se permiten varias mesas con modalidades diferentes en la misma localidad. No se permitirá reservar mientras se selecciona visualmente en el mapa: la reserva empieza al crear la compra existente. No se añade un endpoint de retención previa ni otro temporizador.

## 3. Esquema propuesto

V12 incorpora cuatro tablas nuevas; 31 tablas y 235 columnas verificadas en PostgreSQL temporal. IDs nuevos BIGINT GENERATED ALWAYS AS IDENTITY; fechas TIMESTAMPTZ UTC; FK históricas ON DELETE RESTRICT. Ninguna de estas tablas tiene DELETE para el rol de aplicación.

### distribuciones_evento

| Columna | Definición propuesta |
|---|---|
| id | BIGINT identity PK |
| evento_id | INT NOT NULL FK eventos |
| localidad_id | INT NOT NULL FK localidades |
| estado | VARCHAR(20) NOT NULL DEFAULT BORRADOR; BORRADOR/PUBLICADA |
| imagen_plano_url | VARCHAR(255) NULL; referencia congelada al publicar, no carga arbitraria de URL externa |
| fecha_creacion | TIMESTAMPTZ NOT NULL DEFAULT now() |
| fecha_publicacion | TIMESTAMPTZ NULL |

UNIQUE(evento_id, localidad_id). Validar localidad del recinto del evento. Estado y fecha coherentes mediante CHECK. PUBLICADA implica inventario completo y listo para vender; no se vende antes. No hay edición estructural después de publicar en esta primera versión, incluso si luego se cancelan todas las reservas. Las localidades existentes sin distribución conservan venta general.

### mesas_evento

| Columna | Definición propuesta |
|---|---|
| id | BIGINT identity PK |
| distribucion_id | BIGINT NOT NULL FK distribuciones_evento |
| etiqueta | VARCHAR(50) NOT NULL; ejemplo Mesa 7 |
| modalidad | VARCHAR(20) NOT NULL; COMPLETA/POR_SILLA |
| tipo_entrada_id | INT NOT NULL FK tipos_entrada |
| x, y | NUMERIC(10,6) NOT NULL; coordenadas normalizadas 0..1 |

UNIQUE(distribucion_id, etiqueta), texto no vacío. Capacidad derivada del número de sillas: no mantener otro contador editable. Mesa completa exige al menos una silla; si se desea retirar una silla por operativa, debe resolverse en BORRADOR antes de publicar.

### asientos_evento

| Columna | Definición propuesta |
|---|---|
| id | BIGINT identity PK |
| distribucion_id | BIGINT NOT NULL FK distribuciones_evento |
| mesa_id | BIGINT NULL FK mesas_evento |
| tipo_entrada_id | INT NOT NULL FK tipos_entrada |
| etiqueta | VARCHAR(50) NOT NULL; ejemplo A-04 o M07-S02 |
| fila | VARCHAR(50) NULL |
| x, y | NUMERIC(10,6) NOT NULL; coordenadas normalizadas 0..1 |
| estado | VARCHAR(20) NOT NULL DEFAULT DISPONIBLE; DISPONIBLE/RESERVADO/VENDIDO/BLOQUEADO |

UNIQUE(distribucion_id, etiqueta). Índices por distribución/estado y mesa. CHECK coordenadas y etiqueta no vacía. Si tiene mesa, debe coincidir distribución y tipo de entrada; tipo debe pertenecer al evento/localidad de la distribución. Las relaciones entre tablas requieren FK compuestas o triggers de integridad; no confiar únicamente en C#.

BLOQUEADO es una exclusión operativa, no otra venta. Solo se bloquean sillas disponibles; para mesa COMPLETA se bloquea/desbloquea toda la mesa atómicamente. No se bloquean posiciones reservadas o vendidas. Un cambio operativo no borra posiciones ni altera su etiqueta.

### asignaciones_asiento

| Columna | Definición propuesta |
|---|---|
| id | BIGINT identity PK |
| asiento_id | BIGINT NOT NULL FK asientos_evento |
| detalle_compra_id | INT NOT NULL FK detalle_compra |
| ordinal_boleto | INT NOT NULL CHECK > 0; posición dentro del detalle |
| estado | VARCHAR(20) NOT NULL DEFAULT RESERVADA; RESERVADA/CONFIRMADA/LIBERADA |
| boleto_id | BIGINT NULL FK boletos, UNIQUE cuando no NULL |
| fecha_reserva | TIMESTAMPTZ NOT NULL DEFAULT now() |
| fecha_confirmacion | TIMESTAMPTZ NULL |
| fecha_liberacion | TIMESTAMPTZ NULL |

UNIQUE(detalle_compra_id, ordinal_boleto); ordinal <= cantidad del detalle. Índice UNIQUE parcial por asiento_id WHERE estado IN ('RESERVADA','CONFIRMADA'): como máximo una asignación vigente por silla. La reserva expira con compras.expira_en, no con un segundo plazo propio.

RESERVADA sin boleto ni fecha de confirmación/liberación; CONFIRMADA con boleto y fecha de confirmación; LIBERADA con fecha de liberación, conserva boleto/confirmación si existieron. Boleto debe pertenecer al mismo detalle. Eventos, localidades y tipos deben coincidir en toda la cadena. Un constraint trigger diferido verifica al COMMIT la correspondencia del estado del asiento con su asignación vigente y el estado del boleto. USADO conserva CONFIRMADA/VENDIDO; no hay reutilización después de asistencia.

El historial se conserva al liberar y vender de nuevo: nueva asignación, nunca sobrescribir la anterior. El precio histórico sigue en detalle_compra, y las etiquetas de la distribución publicada quedan inmutables. No se utiliza datos_extra JSONB para sustituir relaciones o unicidad.

## 4. Cupos y dinero

- Para tipo numerado, cupo_total = sillas asignadas al tipo, incluidas las bloqueadas. cupo_disponible = sillas DISPONIBLE. Reservadas, vendidas y bloqueadas no están disponibles. Contadores derivados del inventario, nunca editados arbitrariamente por el usuario.
- La suma de sillas numeradas respeta capacidad de localidad y recinto; los tipos generales conservan las reglas actuales.
- Publicar requiere reconciliar todos los tipos de la localidad, capacidad y max_boletos_por_compra. Ningún tipo general puede haber vendido/reservado cupo de esa localidad antes de convertirla.
- Reservar/liberar/bloquear actualiza posiciones y cupo en una misma transacción. Un fallo revierte todo.
- Primera versión: precio uniforme por tipo, como hoy. Mesa de seis a Q100 por silla = Q600 subtotal, Q60 comisión, Q79.20 IVA y Q739.20 total con configuración inicial D-15.
- No reembolso parcial nuevo. El reembolso total conserva las reglas financieras e importes originales.

## 5. Compra, concurrencia y emisión

Extender items de POST /api/v1/compras con asientoIds opcional. General: sin posiciones, cantidad actual. Numerada: posiciones obligatorias, cantidad exactamente igual a su número. En mesa COMPLETA deben aparecer todas sus sillas exactamente una vez, bajo el mismo tipo; el backend verifica la expansión, no confía en la UI. Duplicados incluso entre ítems => 400. Posición ajena al evento/tipo => 422; ocupada/bloqueada => 409. Una compra puede mezclar zonas generales y numeradas del mismo evento.

Reserva: validar actor, idempotencia, evento y venta; bloquear tipos en orden estable como hoy; adquirir posiciones con UPDATE condicional DISPONIBLE -> RESERVADO, comprobar todas las filas; guardar compra/detalles/asignaciones, decrementar cupos y escribir auditoría/outbox en la misma transacción. Si falta una silla, rollback de toda la compra. Mantener orden de bloqueos compartido por compra, liberación y operaciones administrativas para evitar ciclos; concretarlo y probarlo antes del SQL definitivo.

La repetición propia de Idempotency-Key conserva la respuesta original existente, sin otra reserva. La UI genera una nueva clave si cambia la intención de compra. No cambiar silenciosamente la semántica histórica de la API.

Conservar orden explícito posición-asistente mediante ordinal dentro del detalle. La normalización actual que agrupa por tipo debe preservar esa asociación. El Worker lee posiciones persistidas; RabbitMQ sigue transportando publicId y no datos personales. El outbox existente conserva asistentes como hoy.

Al completar: pago/ledger/saldos/boletos/confirmación de asignaciones/RESERVADO -> VENDIDO/compra COMPLETADA/outbox en una transacción. Un reintento verifica correspondencia íntegra de boletos y asignaciones; no reasigna ni duplica. QR HMAC existente: la etiqueta se obtiene del servidor; no confiar en el texto que envía el cliente.

Al cancelar/expirar/fallar: solo el ganador de la transición de compra libera sus asignaciones RESERVADA, cambia sus sillas a DISPONIBLE y devuelve cupo. Pago incierto o recuperación sin resultado confirmado conserva reserva PROCESANDO; nunca liberar simplemente por vencimiento del reloj.

Reembolso: solo boletos VALIDO -> ANULADO liberan su asignación CONFIRMADA y silla VENDIDO; boletos USADO conservan asignación e inventario. Todo junto con ledger/pago/auditoría. Un proceso viejo debe comprobar asignación/compra propietaria antes de liberar. Cancelar un evento no implica reembolso ni liberación masiva automática.

## 6. Contratos aprobados para implementación

Todas las rutas siguientes son contratos nuevos aprobados, implementados en EXT-NUM-03. ADMINISTRADOR habilitado o DUENO_RECINTO propietario para configurar, propiedad validada en Application; ajena/inexistente 404. No nuevas capacidades para CLIENTE.

| Método y ruta | Función |
|---|---|
| PUT /api/v1/eventos/{eventoId}/localidades/{localidadId}/distribucion | Crear/editar contenido BORRADOR; IDs persistentes para filas existentes y claves temporales para nuevas; validar referencias, límites y payload completo |
| GET /api/v1/eventos/{eventoId}/localidades/{localidadId}/distribucion | Leer configuración privada, incluso BORRADOR; mesas/sillas paginadas con page/pageSize |
| POST /api/v1/eventos/{eventoId}/localidades/{localidadId}/distribucion/publicar | Congelar estructura, validar capacidad/cupos y habilitar inventario |
| GET /api/v1/eventos/{eventoId}/localidades/{localidadId}/asientos | Disponibilidad pública solo para eventos visibles y distribución publicada; page/pageSize, filtro mesaId; sin datos de compradores/compras |
| POST /api/v1/eventos/{eventoId}/localidades/{localidadId}/asientos/bloquear | Bloqueo operativo de lista de IDs; mesa completa se trata como unidad; auditoría |
| POST /api/v1/eventos/{eventoId}/localidades/{localidadId}/asientos/desbloquear | Operación inversa sobre BLOQUEADO, validación y auditoría |

PUT en BORRADOR incorpora filas nuevas y edita las existentes por ID, sin eliminar filas ni reutilizar IDs. Debe incluir las filas existentes y rechazar su omisión para evitar borrados implícitos. Las sillas que no se ofrecerán se marcan BLOQUEADO y siguen contando en la capacidad física. Etiquetas y pertenencia a mesa/tipo solo se editan antes de publicar; mover una silla requiere validar las dos mesas. Las mesas persistidas no se borran y deben conservar al menos una silla al publicar. El tamaño máximo de configuración se fijará de acuerdo con las capacidades admitidas y se documentará en el contrato antes de implementar.

Límites iniciales del contrato: hasta 2000 sillas y 500 mesas por distribución; payload JSON máximo 2 MiB para configuración; pageSize 1–100, page 1–100000, default 20. La publicación rechaza configuraciones que no respeten las capacidades, aunque estén bajo estos límites. El consumo de listas grandes será paginado. Estos límites técnicos se validarán en EXT-NUM-03 y no alteran la capacidad de localidades generales.

Extensiones de rutas existentes: POST compras admite asientoIds; GET compra, mis boletos y respuesta de validación muestran ubicación; detalle público de evento anuncia modalidad por localidad. El mapa base del recinto sigue existiendo y se superpone la distribución del evento. No crear otra ruta de pago ni otra cola.

Listados siempre paginados. El frontend debe cargar todas las páginas necesarias; una silla ausente por paginación no equivale a ocupada. Disponibilidad no se cachea como garantía. Usar polling acotado/reconsulta tras 409; no se propone WebSocket para esta primera versión.

## 7. Compatibilidad y despliegue

Las entradas/boletos actuales continúan sin posición. No convertir eventos con historial de compras a numerados. La distribución solo se publica para evento/localidad sin compras previas de esa zona. Después de publicar se impiden cambios estructurales, traslado de localidad, renumeración y edición manual de cupos. Validar también cambios de recinto del evento y bajas de sus padres.

Crear V12__localidades_numeradas.sql solo si V11 sigue siendo la última al empezar, con índices, FKs, triggers y permisos propios. No tocar V1–V11. Añadir mapeos EF manuales y puertos. API y Worker antiguos no conocen las nuevas reservas: activar venta numerada únicamente cuando ambos estén actualizados; no permitir workers antiguos consumiendo compras numeradas. Durante despliegue mixto mantener esa venta deshabilitada.

Actualizar conteos/ER/diccionario, D-08, reglas de reserva y plan. Revisar vistas bi y reportes: operaciones por boleto/persona, no contar una mesa como una entrada. No cambiar bi sin migración. Actualizar documentos públicos/RAG que afirman que nunca hay sillas numeradas y publicar nuevas versiones; no dejar las antiguas como fuentes vigentes contradictorias.

## 8. Bloque de tareas propuesto

IDs EXT-NUM para no fingir que son tareas originales de una fase cerrada. Orden excepcional aprobado antes de F10, sin reabrir casillas completadas ni adelantar reportes/Power BI.

| ID | Trabajo y criterio de salida |
|---|---|
| EXT-NUM-01 | Cerrar propuesta y registrar D-08, esquema/contratos y decisiones en AGENTS, BASE_DE_DATOS y PLAN; sin código funcional |
| EXT-NUM-02 | Migración nueva, permisos/invariantes, mapeos y dominio; generales existentes compatibles |
| EXT-NUM-03 | Configuración, publicación, disponibilidad, bloqueos y ownership; Swagger y auditoría |
| EXT-NUM-04 | Compra mixta/reserva atómica/idempotencia/asociación de asistentes; sin venta numerada pública hasta integrar Worker |
| EXT-NUM-05 | Emisión/consulta/QR, cancelación/expiración/fallo/recuperación/reembolso, todo el ciclo; habilitación solo al completar |
| EXT-NUM-06 | Selección frontend, mesa completa/individual, 409 y etiquetas; sin tocar trabajo ajeno pendiente |
| EXT-NUM-07 | Pruebas integradas, revisión de diff, guías demo, RAG y PR |

PRs pequeños sugeridos: A documentación; B esquema+configuración; C ciclo de compra completo; D integración frontend y cierre. Cada uno desde develop actualizado tras merge del anterior. Un commit por tarea con Refs y mensaje en español; mostrar status/mensaje antes. Ningún push/merge por el agente.

## 9. Pruebas esenciales

1. Dos clientes disputan una silla: una reserva, un 409, cupo correcto.
2. Una silla ocupada en una selección múltiple: ninguna reserva parcial, incluso compra mixta.
3. Mesa COMPLETA: selección parcial rechazada; mesa POR_SILLA: compras independientes válidas.
4. Cancelar/expirar/procesar compiten: una liberación o una venta; reintento viejo no libera otra reserva.
5. Mensaje/clave duplicados: mismos boletos, asignaciones y ledger; rollback ante fallo de emisión.
6. Reembolso repetido con boletos usados y válidos: solo libera los válidos anulados.
7. Dos eventos con posiciones equivalentes: inventarios independientes; protección de FK/ownership y exposición pública sin identidad.
8. Regresión general, importes D-15, máximo por personas, bloqueo de mesa completo y cupo conciliado.

Pruebas focalizadas por cambio y una suite completa al cierre; build -warnaserror y formato, sin llamadas reales a IA ni pagos externos.
