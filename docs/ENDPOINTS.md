# Inventario de endpoints — Fase 9 y ampliaciones

Inventario generado desde Swagger de la API compilada. Hay 80 operaciones REST; health checks y Swagger son rutas técnicas adicionales. Autenticado indica Bearer, no sustituye comprobar rol y propiedad en cada operación. Consultar la descripción de Swagger y ANALISIS_FRONTEND.md para los flujos.

La tabla original contiene las 80 operaciones de F9. EXT-NUM añade seis y F10 añade una: **87 operaciones REST** en total. Las ampliaciones se detallan al final; los tres formatos de reportes pertenecen a la misma ruta GET.

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| POST | `/api/v1/auth/login` | Público |  |
| POST | `/api/v1/auth/logout` | Autenticado |  |
| POST | `/api/v1/auth/refresh` | Público |  |
| POST | `/api/v1/auth/registro` | Público |  |
| GET | `/api/v1/boletos` | Autenticado | Mis boletos con token QR; incluye VALIDO, USADO y ANULADO |
| POST | `/api/v1/boletos/validar` | Autenticado | Validar firma y registrar una única entrada VALIDO → USADO |
| GET | `/api/v1/catalogo/eventos` | Público | Catálogo público: PROGRAMADO/EN_VENTA/AGOTADO, filtros combinables y paginación |
| GET | `/api/v1/catalogo/eventos/{id}` | Público | Detalle público con recinto, categoría, presentadores y tipos activos; no contiene usuarios ni finanzas |
| GET | `/api/v1/categorias` | Autenticado | Listar catálogo paginado, incluidos inactivos; requiere sesión completa |
| POST | `/api/v1/categorias` | Autenticado | Crear categoría; solo administrador |
| DELETE | `/api/v1/categorias/{id}` | Autenticado | Baja lógica idempotente; conserva referencias e historial |
| GET | `/api/v1/categorias/{id}` | Autenticado |  |
| PUT | `/api/v1/categorias/{id}` | Autenticado |  |
| GET | `/api/v1/compras` | Autenticado | Mis compras, incluidas las canceladas; paginación de 1 a 100 filas |
| POST | `/api/v1/compras` | Autenticado | Reservar compra PENDIENTE; precios del catálogo y clave idempotente obligatoria |
| GET | `/api/v1/compras/{publicId}` | Autenticado | Consultar una compra propia por publicId; polling de su estado |
| POST | `/api/v1/compras/{publicId}/cancelar` | Autenticado | Cancelar compra propia PENDIENTE y devolver cupo una sola vez |
| POST | `/api/v1/compras/{publicId}/reembolsar` | Autenticado | Reembolsar completamente una compra; solo administrador habilitado |
| GET | `/api/v1/cuentas` | Autenticado | Consultar cuentas propias o todas como administrador |
| GET | `/api/v1/cuentas/{id}` | Autenticado | Consultar estado y saldo de una cuenta |
| POST | `/api/v1/cuentas/{id}/cerrar` | Autenticado | Cerrar una cuenta de dueño con saldo cero |
| POST | `/api/v1/cuentas/{id}/liquidar` | Autenticado | Registrar una liquidación al dueño |
| GET | `/api/v1/cuentas/{id}/movimientos` | Autenticado | Consultar extracto paginado de movimientos de una cuenta |
| GET | `/api/v1/eventos` | Autenticado | Gestión privada paginada; dueño solo sus eventos, administrador todos |
| POST | `/api/v1/eventos` | Autenticado | Crear BORRADOR; creador de la sesión, dueño del recinto o administrador |
| GET | `/api/v1/eventos/{eventoId}/presentadores` | Autenticado |  |
| POST | `/api/v1/eventos/{eventoId}/presentadores` | Autenticado | Asociar presentador activo a evento propio BORRADOR/PROGRAMADO sin compras |
| DELETE | `/api/v1/eventos/{eventoId}/presentadores/{presentadorId}` | Autenticado | Retirar asociación idempotentemente; conserva evento y presentador |
| PUT | `/api/v1/eventos/{eventoId}/presentadores/{presentadorId}` | Autenticado |  |
| GET | `/api/v1/eventos/{eventoId}/tipos-entrada` | Autenticado |  |
| POST | `/api/v1/eventos/{eventoId}/tipos-entrada` | Autenticado | Configurar entrada general, por localidad o distancia; disponibilidad inicial = cupo total |
| DELETE | `/api/v1/eventos/{eventoId}/tipos-entrada/{id}` | Autenticado | Baja lógica antes de ventas; sigue contando para la capacidad de su localidad |
| GET | `/api/v1/eventos/{eventoId}/tipos-entrada/{id}` | Autenticado |  |
| PUT | `/api/v1/eventos/{eventoId}/tipos-entrada/{id}` | Autenticado | Editar antes de ventas; ajuste atómico de cupo, localidad inmutable |
| DELETE | `/api/v1/eventos/{id}` | Autenticado | Cancelar sin compras; baja lógica idempotente, no reembolsa |
| GET | `/api/v1/eventos/{id}` | Autenticado |  |
| PUT | `/api/v1/eventos/{id}` | Autenticado | Configurar BORRADOR/PROGRAMADO sin compras; recinto y creador inmutables |
| PUT | `/api/v1/eventos/{id}/estado` | Autenticado | Transición condicional; venta con ventana vigente y cupo |
| POST | `/api/v1/eventos/{id}/imagen` | Autenticado | Subir imagen a BORRADOR/PROGRAMADO sin compras; propietario o administrador |
| GET | `/api/v1/eventos/{id}/resenas` | Público | Reseñas APROBADA de un evento; acceso público |
| POST | `/api/v1/ia/consultas` | Público | Preguntar a José sobre información pública con fuentes |
| GET | `/api/v1/ia/documentos` | Autenticado | Listar versiones de documentos; solo administrador |
| POST | `/api/v1/ia/documentos` | Autenticado | Cargar documento público del negocio para RAG; solo administrador |
| GET | `/api/v1/ia/documentos/{id}` | Autenticado | Consultar texto extraído y versiones; solo administrador |
| POST | `/api/v1/ia/documentos/{id}/desactivar` | Autenticado | Desactivar una versión sin borrar documento, texto o chunks |
| POST | `/api/v1/ia/documentos/{id}/reindexar` | Autenticado | Indexar o reindexar documento público; administrador habilitado |
| POST | `/api/v1/ia/documentos/{id}/versiones` | Autenticado | Crear una nueva versión sin sobrescribir el contenido anterior |
| GET | `/api/v1/imagenes/{nombre}` | Público | Leer PNG solo si está referenciado por un recurso visible públicamente |
| GET | `/api/v1/presentadores` | Autenticado | Listar catálogo paginado, incluidos inactivos; requiere sesión completa |
| POST | `/api/v1/presentadores` | Autenticado | Crear presentador; solo administrador |
| DELETE | `/api/v1/presentadores/{id}` | Autenticado | Baja lógica idempotente; conserva referencias e historial |
| GET | `/api/v1/presentadores/{id}` | Autenticado |  |
| PUT | `/api/v1/presentadores/{id}` | Autenticado |  |
| GET | `/api/v1/presentadores/{id}/calificacion` | Público | Promedio y cantidad de reseñas aprobadas por presentador |
| POST | `/api/v1/presentadores/{id}/imagen` | Autenticado | Subir imagen de presentador activo; administrador |
| GET | `/api/v1/recintos` | Autenticado | Listar recintos propios; administrador ve todos, incluidos inactivos |
| POST | `/api/v1/recintos` | Autenticado | Crear recinto propio o asignarlo a un dueño como administrador |
| DELETE | `/api/v1/recintos/{id}` | Autenticado | Dar de baja lógicamente; repetir la baja conserva éxito sin duplicar auditoría |
| GET | `/api/v1/recintos/{id}` | Autenticado |  |
| PUT | `/api/v1/recintos/{id}` | Autenticado | Editar recinto activo sin cambiar su dueño; capacidad incluye localidades inactivas |
| POST | `/api/v1/recintos/{id}/imagen-plano` | Autenticado | Subir plano PNG/JPEG; dueño del recinto o administrador, recurso activo |
| GET | `/api/v1/recintos/{id}/mapa` | Público | Mapa público del recinto activo; eventoId opcional añade precios y cupos |
| GET | `/api/v1/recintos/{recintoId}/localidades` | Autenticado | Listar zonas del recinto, incluidas inactivas, con paginación |
| POST | `/api/v1/recintos/{recintoId}/localidades` | Autenticado | Crear zona del recinto propio o como administrador; suma de capacidades protegida |
| DELETE | `/api/v1/recintos/{recintoId}/localidades/{id}` | Autenticado | Baja lógica idempotente; la capacidad de esta zona sigue contando |
| GET | `/api/v1/recintos/{recintoId}/localidades/{id}` | Autenticado |  |
| PUT | `/api/v1/recintos/{recintoId}/localidades/{id}` | Autenticado | Editar zona activa, polígono y color sin cambiar recinto; protege cupos por evento |
| GET | `/api/v1/resenas` | Autenticado | Cola administrativa de reseñas; PENDIENTE por defecto |
| POST | `/api/v1/resenas` | Autenticado | Crear reseña PENDIENTE tras asistir a un evento |
| PUT | `/api/v1/resenas/{id}` | Autenticado | Editar reseña propia y devolverla a revisión |
| PATCH | `/api/v1/resenas/{id}/moderar` | Autenticado | Aprobar o rechazar reseña PENDIENTE |
| POST | `/api/v1/resenas/{id}/retirar` | Autenticado | Retirar reseña propia sin borrar la fila |
| GET | `/api/v1/resenas/mis` | Autenticado | Mis reseñas en todos sus estados, incluidas retiradas |
| GET | `/api/v1/usuarios` | Autenticado |  |
| DELETE | `/api/v1/usuarios/{id}` | Autenticado |  |
| GET | `/api/v1/usuarios/{id}` | Autenticado |  |
| PUT | `/api/v1/usuarios/{id}` | Autenticado |  |
| PUT | `/api/v1/usuarios/{id}/password` | Autenticado |  |
| POST | `/api/v1/usuarios/administradores` | Autenticado |  |
| POST | `/api/v1/usuarios/duenos-recinto` | Autenticado |  |

## Ampliación EXT-NUM-03: seis operaciones de distribución

El inventario previo contiene 80 operaciones; estas seis incorporan un total de 86. Rutas relativas a `/api/v1/eventos/{eventoId}/localidades/{localidadId}`:

| Método/ruta | Acceso | Función |
|---|---|---|
| PUT /distribucion | Admin/dueño propietario habilitado | Crear/editar borrador, máximo 2000 sillas/500 mesas, 2 MiB |
| GET /distribucion | Admin/dueño propietario habilitado | Configuración paginada, incluso borrador |
| POST /distribucion/publicar | Admin/dueño propietario habilitado | Congelar configuración y conciliar cupo |
| GET /asientos | Público | Disponibilidad paginada, filtro mesaId, solo evento visible/publicada |
| POST /asientos/bloquear | Admin/dueño propietario habilitado | Lista de IDs; mesa COMPLETA indivisible |
| POST /asientos/desbloquear | Admin/dueño propietario habilitado | Lista de IDs bloqueados; restaura disponibilidad |

page 1–100000, pageSize 1–100 (default 20). 400 validación, 401 autenticación, 403 rol, 404 ajena/inexistente/no pública, 409 conflicto, 413 tamaño. Compra numerada pendiente de integración EXT-NUM-04–05. El diseño y contrato de cuerpos están en LOCALIDADES_NUMERADAS.md y Swagger.

## Ampliación F10: reportes administrativos

| Método/ruta | Acceso | Función |
|---|---|---|
| GET `/api/v1/reportes/{codigo}` | ADMINISTRADOR habilitado | R-01…R-15; filtros, agrupaciones y totales por moneda; `formato=json|xlsx|pdf` |

JSON paginado; XLSX/PDF contienen todo el conjunto filtrado y auditan EXPORT. 400 contrato/filtro incompatible, 401 anónimo, 403 rol/usuario inhabilitado, 404 reporte no disponible, 422 límite de exportación. Swagger declara los tres tipos de respuesta. Guía: [REPORTES.md](REPORTES.md). Power BI se conecta directamente a vistas `bi` con su credencial restringida; ver [POWER_BI.md](POWER_BI.md), aceptación visual pendiente del usuario.
