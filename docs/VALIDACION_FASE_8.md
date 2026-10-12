# Validación de Fase 8 — Reseñas

## Alcance implementado

- F8-01: creación como CLIENTE habilitado con boleto USADO del evento; una por evento/cliente, PENDIENTE, calificación 1–5 y comentario opcional máximo 510 caracteres.
- F8-02: cola administrativa paginada y moderación desde PENDIENTE, aprobación/rechazo condicional, repetición consistente y auditoría.
- F8-03: listado público paginado de APROBADA por evento, sin identidad del cliente.
- F8-04: promedio/cantidad de APROBADA por presentador mediante evento_presentadores; suma/cantidad en un snapshot, media por reseña y cero aprobadas como promedio null.
- F8-05: mis reseñas, edición/retirada solo por autor, vuelta a revisión, conservación de fila/unicidad/fechaCreacion y auditoría en la misma transacción.

Contratos, cuerpos, roles y prueba manual completa en [RESENAS.md](RESENAS.md). La API registra ocho operaciones nuevas, para 73 en Swagger al cerrar el bloque.

## Evidencia de pruebas

Resultado final: **454 pruebas Release aprobadas, 0 fallidas, 0 omitidas**, build con cero warnings/errores y dotnet format limpio. Son 34 casos nuevos: 22 Domain y 12 API sobre PostgreSQL real. Toda la suite, incluidas arquitectura, RabbitMQ, concurrencia y finanzas, permanece en verde.

Los escenarios API usan PostgreSQL temporal migrado con Flyway, ventas completadas mediante el proveedor simulado durable y validación de QR real. No se fabrican reseñas elegibles ni JWT para sustituir el recorrido de negocio. Las contraseñas de login de los usuarios temporales se generan en memoria y se guardan únicamente como hash en su base temporal.

Se prueban límites 510/511 y nota 1–5; sin boleto USADO/evento ajeno/roles incorrectos; diez creaciones simultáneas con una fila y auditoría; dos administradores con decisiones opuestas; publicación por estado/evento y paginación; promedios y co-presentadores; propiedad y sesiones anteriores a baja/cambio obligatorio; edición que retira la publicación anterior, nueva moderación, retiro/reenvío; competencia de edición/retiro y rollback de ambas operaciones ante fallo de auditoría.

El retiro utiliza la acción UPDATE admitida por V7 y el motivo ResenaRetirada en detalle JSONB. La edición usa ResenaEditada; la moderación MODERATE con ResenaAprobada/ResenaRechazada. El comentario no se registra en el detalle de auditoría. No se modificó el CHECK ni ninguna migración para añadir acciones.

## Revisión frente al criterio de salida y AGENTS.md §§5/12

El criterio de salida es «reseñas moderadas y calificación por artista/equipo visible».

- Domain contiene contenido, estados y media; Application usa puertos y handlers separados, sin llamar handlers de otros módulos. Infrastructure implementa EF/SQL y WebApi llama a handlers, sin DbContext en controllers.
- La creación obtiene cliente desde sesión, comprueba asistencia y conserva la unicidad incluso tras retiro. Cliente activo y propiedad se verifican en Application además de policies HTTP.
- Cambios de estado usan WHERE estado esperado bajo bloqueo de fila y comparten transacción con auditoría; reintentos de estado ya alcanzado no duplican efectos.
- Publicación/promedios filtran APROBADA. Consultas propias y cola requieren permisos; respuestas públicas no incluyen clienteId/nombre/email.
- Sin cambios de cupo, pagos, ledger, mensajería o esquema bi; sin DELETE, migraciones editadas, paquetes nuevos, secretos, binarios o dependencias prohibidas. UTC/CT en operaciones async, mapeo manual.
- Rutas aprobadas y ejemplos/errores en Swagger; comentarios limitados a 510; documentación cruzada y casillas del plan actualizadas por tarea.

Límites funcionales del esquema aprobado: retirada y rechazo comparten RECHAZADA; una fila por cliente/evento se conserva para reenviar mediante edición. estadoEsperado controla transiciones de estado; las ediciones sucesivas dentro de PENDIENTE dejan el último contenido vigente, sin historial de versiones del texto. El promedio corresponde a reseñas de eventos asociados, no a una valoración independiente enviada directamente a un presentador. Paginación operativa sin snapshot entre peticiones.

Fase 9 queda sin iniciar por instrucción expresa del usuario hasta que indique qué utilizará para IA. La verificación de CI remoto corresponde a la publicación del PR.

**Incumplimientos detectados en el diff frente al criterio de salida y AGENTS.md §§5/12: ninguno.** Las cinco tareas quedan completas localmente, con un commit por tarea y casilla/evidencia en el mismo commit.
