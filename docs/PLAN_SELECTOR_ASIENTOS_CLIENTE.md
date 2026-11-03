# Plan: selección de asientos específicos en la compra

Estado: integración implementada en el frontend React existente. Este documento conserva la verificación y el plan que guiaron el trabajo; la compilación y pruebas quedan pendientes.

## Verificación inicial

- El mapa público de `frontend-Promotora` dibuja las geometrías de las localidades y permite continuar con una localidad. No muestra sillas.
- `App.tsx` recibe la localidad elegida, pero no conserva su `localidadId` para la compra; abre el checkout con los tipos de entrada del mock del evento.
- `CheckoutModal.tsx` permite elegir cantidades por tipo de entrada y envía `tipoEntradaId`, `cantidad` y, para carreras, los datos de asistentes. No envía `asientoIds`.
- `AdminSeating.tsx` sí deja seleccionar una silla concreta, pero el propósito es bloquearla o desbloquearla como administrador; no reserva ni vende esa silla a un cliente.
- La API ya ofrece `GET /api/v1/eventos/{eventoId}/localidades/{localidadId}/asientos`, público y paginado. Responde un `DistribucionDto` con mesas, asientos, estado y metadatos de paginación.
- La creación de compra ya acepta `AsientoIds`. El backend comprueba que las sillas correspondan a la localidad y al tipo de entrada, que estén disponibles, que la cantidad coincida y que se cumpla la modalidad de la mesa; reserva las posiciones en la transacción y responde conflicto si otra compra ganó la carrera.
- Los tipos React están desalineados con ese contrato: `getAsientos()` espera un arreglo de `SillaDto`, pero la API devuelve `DistribucionDto`; también espera `numero`, mientras el DTO backend publica `etiqueta`. El contrato de compra React aún no define `asientoIds`.
- `docs/LOCALIDADES_NUMERADAS.md` documenta el bloque EXT-NUM y una prueba local en Vue; esa prueba no implementa la selección de compra en la aplicación React versionada.

Conclusión inicial: antes de esta integración, el comprador no podía elegir un asiento específico desde la interfaz pública.

## Resultado implementado

- El panel de distribución usa la geometría poligonal de `localidades.geometria` como lienzo, permite agregar sillas, filas o mesas en el área, moverlas dentro del polígono, guardar el borrador y publicar el plano.
- El cliente API usa el contrato real paginado para editar y consultar asientos, y envía `asientoIds` con la cantidad y tarifa correspondiente.
- El checkout dibuja las sillas publicadas y sus estados, impide elegir inventario ocupado y trata una mesa `COMPLETA` como unidad. La compra crea la reserva atómica existente del backend; seleccionar en el plano no crea una retención separada.
- El conflicto de disponibilidad actualiza el plano y conserva únicamente selecciones todavía libres; las mesas completas parciales se limpian por completo.
- No se instaló una dependencia externa llamada `geometry`: el proyecto ya guarda polígonos vectoriales, y la integración reutiliza sus coordenadas SVG con una función local de punto en polígono.

## Objetivo

Integrar la selección de sillas publicadas en el flujo de compra actual, respetando el inventario y las reglas ya impuestas por el backend. Mantener la compra por cantidad para localidades generales sin distribución numerada.

D-09 para este flujo queda resuelta al integrar la experiencia en el frontend React versionado. El prototipo local Vue no se usa como dependencia de producción.

## Alcance y pasos

### 1. Alinear los contratos del frontend

- Modelar los DTO reales: distribución paginada, `mesas` (`id`, `etiqueta`, `modalidad`, `tipoEntradaId`, coordenadas, `capacidad`) y `asientos` (`id`, `mesaId`, `etiqueta`, `fila`, `tipoEntradaId`, coordenadas, `estado`).
- Cambiar el cliente API para recibir `DistribucionDto` y aceptar `page`, `pageSize` y `mesaId` en la consulta pública.
- Cargar todas las páginas requeridas para mostrar la localidad completa, o consultar por mesa cuando el plano sea grande; no asumir que la primera página contiene todos los asientos.
- Agregar `asientoIds?: number[]` al tipo de ítem de compra y conservar el resto del contrato, incluida la asociación de asistentes.

### 2. Llevar la localidad elegida hasta el checkout

- Guardar evento y `localidadId` seleccionados desde `VenueMapModal`; no inferirlos de nombres ni de los datos mock.
- Consultar disponibilidad pública para esa localidad antes de mostrar el selector.
- Si hay distribución publicada, mostrar selección numerada. Si la localidad es general y no tiene distribución, conservar el checkout actual por cantidad.
- Si la consulta falla por red o permisos, presentar el error y permitir reintentar; no transformar un error de disponibilidad en asientos ficticios.

### 3. Crear la experiencia visual de selección

- Renderizar mesas y sillas usando las coordenadas normalizadas de la distribución y las etiquetas humanas de mesa, fila y silla.
- Permitir seleccionar solo sillas `DISPONIBLE`. Mostrar `RESERVADO`, `VENDIDO` y `BLOQUEADO` como no seleccionables, con leyenda accesible y estados que no dependan solo del color.
- Para mesa `POR_SILLA`, permitir seleccionar sillas individualmente.
- Para mesa `COMPLETA`, aplicar la regla de unidad: seleccionar todas sus sillas disponibles juntas o ninguna; impedir selecciones parciales. Si la mesa ya no está íntegramente libre, deshabilitarla.
- Mostrar localidad, tipo de entrada, etiquetas elegidas, cantidad y precio estimado. El servidor sigue siendo la autoridad del precio y del estado real.
- Permitir quitar una selección y regresar al mapa de localidades sin perder el evento.

### 4. Integrar selección y creación de compra

- Agrupar las sillas seleccionadas por `tipoEntradaId` y enviar para cada tipo `cantidad = asientoIds.length` y sus `asientoIds`.
- Conservar el orden de los IDs y asociar los datos de cada asistente a ese mismo orden, porque las asignaciones de boletos usan un ordinal por asiento.
- Mantener los límites de boletos por compra y validar en el cliente que no haya IDs duplicados; repetir esas reglas en servidor sigue siendo obligatorio.
- Permitir compras mixtas si el flujo existente las ofrece: los tipos generales se envían sin `asientoIds` y los numerados con ellos.
- No reservar asientos al navegar o seleccionar. La reserva comienza al crear la compra; no agregar una retención temporal paralela al modelo backend actual.

### 5. Resolver disponibilidad concurrente

- Si la compra responde `409 Conflict` porque una silla cambió de estado, volver a consultar la distribución, retirar o marcar las sillas que ya no estén disponibles y pedir al cliente que confirme una nueva selección.
- No reintentar automáticamente una compra con una selección que el servidor rechazó.
- Mostrar estados de carga, localidad sin sillas disponibles, error recuperable y conflicto de compra.

## Criterios de aceptación

- En una localidad numerada publicada, el comprador ve las etiquetas reales y puede elegir una o varias sillas disponibles.
- Sillas reservadas, vendidas o bloqueadas no se pueden seleccionar.
- Las mesas `COMPLETA` solo se compran completas; las `POR_SILLA` permiten selección individual.
- El checkout envía `asientoIds` correctos, con cantidad y tipo de entrada coherentes, y conserva la relación con los asistentes.
- Una compra general sigue funcionando sin `asientoIds`; la API existente rechaza una compra numerada que omita los asientos.
- Al haber competencia por una silla, el cliente recibe el conflicto, actualiza disponibilidad y puede volver a seleccionar sin duplicar la compra.
- Un plano con más de una página de resultados se muestra completo y la UI no confunde el ID de silla con su etiqueta visible.
- La experiencia es usable con teclado y lector de pantalla e identifica cada estado con texto además de color.

## Dependencias y límites

- El bloque EXT-NUM-06 queda integrado en React; conservar y revisar por separado los cambios ajenos que ya estaban en el frontend.
- La API no mantiene una reserva mientras el comprador explora el plano. Una silla puede dejar de estar disponible antes del POST de compra; el flujo de conflicto es parte necesaria de la experiencia.
- Los IDs de asientos son `long` en .NET. El frontend debe tratarlos sin pérdida de precisión al enviarlos de vuelta; si el dominio llega a superar el entero seguro de JavaScript, se deberá acordar una representación decimal de cadena en el contrato.
