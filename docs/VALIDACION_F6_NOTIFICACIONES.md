# Revisión F6-05/F6-06

Rama feature/f6-notificaciones desde origin/develop cc314c3, después del merge del PR #20. Un commit por tarea, sin push ni merge por el agente.

## Resultado y evidencia

F6-05 implementa confirmación desde compra.completada: consulta compra COMPLETADA con respaldo financiero y boletos completos; SMTP, persistencia de notificación y deduplicación. Su productor permanece pendiente de F7. F6-06 implementa bienvenida desde el evento real de registro, con destinatario consultado desde BD. No hay endpoints nuevos; los contratos Swagger existentes conservan sus respuestas.

- Build Release con warnaserror: cero errores y warnings.
- Suite completa Release: 256 pruebas en verde, cero omisiones (Domain 39, Application 5, Infrastructure 102, API 98, Architecture 12).
- dotnet format --no-restore --verify-no-changes limpio; git diff --check limpio.
- 17 pruebas nuevas: RabbitMQ/PostgreSQL/MailHog reales, registro→outbox→relay→correo, MIME y boletos, duplicados y veinte entregas concurrentes, destinatario actualizado, compra no completada/sin emisión/sin ledger, SMTP con retry/resultado terminal, cancelación con rollback y mensaje inválido.

## Revisión contra AGENTS.md §§5/12

Puertos y handlers en Application; SMTP/EF/RabbitMQ en Infrastructure; composición y reconexión en Worker. Controllers sin acceso directo a infraestructura. No cambios de reservas, estados de compra, ledger ni movimientos. No DELETE de datos protegidos, migraciones editadas, esquema bi modificado, paquetes nuevos ni secretos versionados. Uso de UTC/IClock, cancelación async y scopes por entrega.

Deduplicación y registro de notificaciones comparten transacción. El ACK ocurre después del commit; fallos inesperados/cancelación dejan el original sin confirmar. SMTP es un efecto externo: una caída entre envío y commit puede duplicar correo. Se documenta sin prometer exactamente una entrega. Cuerpo y QR completos no se guardan en notificaciones ni se registran en logs.

SMTP falla hasta el máximo configurado, guarda FALLIDO y permite ACK; no hay descarte silencioso ni reenvío ilimitado. Sin MessageId válido se conserva el original sin ACK; puede bloquear la cola y requiere corregir el productor. No se añaden colas, endpoints administrativos ni campos fuera del esquema aprobado.

## Pendientes de fase

F6-03/F6-04 y la salida de fase siguen pendientes de finalización financiera F7. F6-07 debe programar expiración; F6-08 debe probar consumidor financiero completo; F6-09 carga, F6-10 diagrama y F6-11 recuperación permanecen pendientes. Este bloque no cierra Fase 6 ni la prueba de venta completada F5-09. Sin incumplimientos detectados dentro del alcance F6-05/F6-06, con los límites de SMTP documentados.

Comandos de configuración, pruebas y Swagger para frontend: [NOTIFICACIONES_CORREO.md](NOTIFICACIONES_CORREO.md).
