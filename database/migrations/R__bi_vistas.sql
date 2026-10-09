-- F1-08: bases de los 15 reportes documentados en BASE_DE_DATOS.md §10.
-- 16 vistas: R-15 utiliza dos. No se agregan tablas ni datos operativos.
-- Suposiciones: dias calculados en UTC; importes nunca mezclan monedas.
-- Ventas R-01/04/09: compras COMPLETADA, importes brutos de la cabecera.
-- Detalles se agregan antes de unir compras para no duplicar dinero.
-- R-02/03 conservan dimensiones de evento para filtrar antes de agrupar en
-- F10. Las vistas son fuentes de reportes, no endpoints ni exportadores.
-- R-04 toma el ultimo pago APROBADO por compra: reintentos no duplican ventas.
-- R-05 cupo ocupado incluye reservas; R-06 distingue emitidos, vigentes y
-- anulados. R-07 distingue ventas/reembolsos/liquidaciones; R-08 usa los
-- movimientos COMISION/REEMBOLSO de PLATAFORMA, con su signo original.
-- R-11 solo resenas APROBADA. R-13 cuenta boletos VALIDO/USADO de Carrera
-- con tipos nombrados 5K/10K/21K/42K; no inventa una columna distancia.
-- Logs se exponen agregados, sin textos, identificadores de sesion ni IPs.
-- bi_reader es un grupo NOLOGIN sin credenciales. Las vistas usan permisos
-- del propietario flyway para que bi_reader no necesite SELECT operativo.
-- Su SELECT se concede solo a estas vistas; no hay DML ni CREATE para BI.

CREATE SCHEMA IF NOT EXISTS bi AUTHORIZATION flyway;
REVOKE ALL ON SCHEMA bi FROM PUBLIC;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'bi_reader') THEN
        CREATE ROLE bi_reader NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;
END; $$;

SET ROLE flyway;

CREATE OR REPLACE VIEW bi.vw_ventas_por_evento AS
SELECT (c.fecha_compra AT TIME ZONE 'UTC')::date AS fecha,
       e.id AS evento_id, e.nombre AS evento_nombre, e.estado AS evento_estado,
       e.categoria_id, e.recinto_id, r.dueno_id, c.estado AS compra_estado, c.moneda,
       count(*) AS compras_completadas,
       sum(coalesce(d.cantidad, 0)) AS boletos_vendidos,
       sum(c.subtotal) AS subtotal, sum(c.impuestos) AS impuestos,
       sum(c.comision_plataforma) AS comision_plataforma, sum(c.total) AS total
FROM public.compras c
JOIN public.eventos e ON e.id = c.evento_id
JOIN public.recintos r ON r.id = e.recinto_id
LEFT JOIN (SELECT compra_id, sum(cantidad) AS cantidad FROM public.detalle_compra GROUP BY compra_id) d ON d.compra_id = c.id
WHERE c.estado = 'COMPLETADA'
GROUP BY (c.fecha_compra AT TIME ZONE 'UTC')::date, e.id, e.nombre, e.estado,
         e.categoria_id, e.recinto_id, r.dueno_id, c.estado, c.moneda;

CREATE OR REPLACE VIEW bi.vw_ventas_por_categoria AS
SELECT v.*, cat.nombre AS categoria_nombre
FROM bi.vw_ventas_por_evento v JOIN public.categorias_evento cat ON cat.id = v.categoria_id;

CREATE OR REPLACE VIEW bi.vw_ventas_por_recinto AS
SELECT v.*, r.nombre AS recinto_nombre
FROM bi.vw_ventas_por_evento v JOIN public.recintos r ON r.id = v.recinto_id;

CREATE OR REPLACE VIEW bi.vw_ventas_diarias AS
SELECT (c.fecha_compra AT TIME ZONE 'UTC')::date AS fecha,
       e.id AS evento_id, e.categoria_id, e.recinto_id, r.dueno_id,
       e.estado AS evento_estado, c.estado AS compra_estado, c.moneda,
       p.metodo, count(*) AS compras_completadas, sum(c.total) AS total
FROM public.compras c JOIN public.eventos e ON e.id = c.evento_id
JOIN public.recintos r ON r.id = e.recinto_id
LEFT JOIN LATERAL (
    SELECT metodo FROM public.pagos WHERE compra_id = c.id AND estado = 'APROBADO'
    ORDER BY fecha DESC, id DESC LIMIT 1
) p ON true
WHERE c.estado = 'COMPLETADA'
GROUP BY (c.fecha_compra AT TIME ZONE 'UTC')::date, e.id, e.categoria_id,
         e.recinto_id, r.dueno_id, e.estado, c.estado, c.moneda, p.metodo;

CREATE OR REPLACE VIEW bi.vw_ocupacion_localidad AS
SELECT e.id AS evento_id, e.nombre AS evento_nombre, e.estado AS evento_estado,
       (e.fecha_inicio AT TIME ZONE 'UTC')::date AS fecha,
       e.categoria_id, e.recinto_id, r.dueno_id, l.id AS localidad_id,
       l.nombre AS localidad_nombre, l.capacidad,
       sum(t.cupo_total) AS cupo_total, sum(t.cupo_disponible) AS cupo_disponible,
       sum(t.cupo_total - t.cupo_disponible) AS cupo_ocupado,
       round(100.0 * sum(t.cupo_total - t.cupo_disponible) / nullif(sum(t.cupo_total), 0), 2) AS porcentaje_ocupacion
FROM public.tipos_entrada t JOIN public.eventos e ON e.id = t.evento_id
JOIN public.recintos r ON r.id = e.recinto_id
JOIN public.localidades l ON l.id = t.localidad_id
GROUP BY e.id, e.nombre, e.estado, e.fecha_inicio, e.categoria_id, e.recinto_id,
         r.dueno_id, l.id, l.nombre, l.capacidad;

CREATE OR REPLACE VIEW bi.vw_asistencia_evento AS
SELECT (b.fecha_emision AT TIME ZONE 'UTC')::date AS fecha,
       e.id AS evento_id, e.nombre AS evento_nombre, e.estado AS evento_estado,
       e.categoria_id, e.recinto_id, r.dueno_id, c.moneda,
       count(*) AS boletos_emitidos,
       count(*) FILTER (WHERE b.estado IN ('VALIDO', 'USADO')) AS boletos_vendidos,
       count(*) FILTER (WHERE b.estado = 'USADO') AS boletos_usados,
       count(*) FILTER (WHERE b.estado = 'ANULADO') AS boletos_anulados
FROM public.boletos b JOIN public.detalle_compra d ON d.id = b.detalle_compra_id
JOIN public.compras c ON c.id = d.compra_id JOIN public.eventos e ON e.id = c.evento_id
JOIN public.recintos r ON r.id = e.recinto_id
GROUP BY (b.fecha_emision AT TIME ZONE 'UTC')::date, e.id, e.nombre, e.estado,
         e.categoria_id, e.recinto_id, r.dueno_id, c.moneda;

CREATE OR REPLACE VIEW bi.vw_ingresos_dueno AS
SELECT (m.fecha AT TIME ZONE 'UTC')::date AS fecha, cf.dueno_id, cf.id AS cuenta_id, cf.moneda,
       e.id AS evento_id, e.categoria_id, e.recinto_id, e.estado AS evento_estado,
       c.estado AS compra_estado,
       coalesce(sum(m.monto) FILTER (WHERE m.tipo = 'VENTA'), 0) AS ventas,
       coalesce(sum(m.monto) FILTER (WHERE m.tipo = 'REEMBOLSO'), 0) AS reembolsos,
       coalesce(sum(m.monto) FILTER (WHERE m.tipo = 'LIQUIDACION'), 0) AS liquidaciones,
       sum(m.monto) AS movimiento_neto
FROM public.movimientos_financieros m JOIN public.cuentas_financieras cf ON cf.id = m.cuenta_id
LEFT JOIN public.compras c ON c.id = m.compra_id LEFT JOIN public.eventos e ON e.id = c.evento_id
WHERE cf.tipo = 'DUENO'
GROUP BY (m.fecha AT TIME ZONE 'UTC')::date, cf.dueno_id, cf.id, cf.moneda,
         e.id, e.categoria_id, e.recinto_id, e.estado, c.estado;

CREATE OR REPLACE VIEW bi.vw_comisiones_plataforma AS
SELECT (m.fecha AT TIME ZONE 'UTC')::date AS fecha, cf.moneda,
       e.id AS evento_id, e.categoria_id, e.recinto_id, r.dueno_id,
       e.estado AS evento_estado, c.estado AS compra_estado,
       coalesce(sum(m.monto) FILTER (WHERE m.tipo = 'COMISION'), 0) AS comisiones,
       coalesce(sum(m.monto) FILTER (WHERE m.tipo = 'REEMBOLSO'), 0) AS reembolsos,
       sum(m.monto) AS comision_neta
FROM public.movimientos_financieros m JOIN public.cuentas_financieras cf ON cf.id = m.cuenta_id
LEFT JOIN public.compras c ON c.id = m.compra_id LEFT JOIN public.eventos e ON e.id = c.evento_id
LEFT JOIN public.recintos r ON r.id = e.recinto_id
WHERE cf.tipo = 'PLATAFORMA' AND m.tipo IN ('COMISION', 'REEMBOLSO')
GROUP BY (m.fecha AT TIME ZONE 'UTC')::date, cf.moneda, e.id, e.categoria_id,
         e.recinto_id, r.dueno_id, e.estado, c.estado;

CREATE OR REPLACE VIEW bi.vw_top_clientes AS
SELECT (c.fecha_compra AT TIME ZONE 'UTC')::date AS fecha, cl.id AS cliente_id,
       u.nombre, u.apellido, e.id AS evento_id, e.categoria_id, e.recinto_id,
       r.dueno_id, e.estado AS evento_estado, c.estado AS compra_estado, c.moneda,
       count(*) AS compras_completadas, sum(c.total) AS total
FROM public.compras c JOIN public.clientes cl ON cl.id = c.cliente_id
JOIN public.usuarios u ON u.id = cl.usuario_id JOIN public.eventos e ON e.id = c.evento_id
JOIN public.recintos r ON r.id = e.recinto_id
WHERE c.estado = 'COMPLETADA'
GROUP BY (c.fecha_compra AT TIME ZONE 'UTC')::date, cl.id, u.nombre, u.apellido,
         e.id, e.categoria_id, e.recinto_id, r.dueno_id, e.estado, c.estado, c.moneda;

CREATE OR REPLACE VIEW bi.vw_compras_no_exitosas AS
SELECT c.public_id AS compra_public_id, c.fecha_compra,
       (c.fecha_compra AT TIME ZONE 'UTC')::date AS fecha, c.estado AS compra_estado,
       c.moneda, c.total, c.cliente_id, e.id AS evento_id, e.categoria_id,
       e.recinto_id, r.dueno_id, e.estado AS evento_estado,
       p.intentos_pago, p.pagos_rechazados
FROM public.compras c JOIN public.eventos e ON e.id = c.evento_id
JOIN public.recintos r ON r.id = e.recinto_id
LEFT JOIN LATERAL (
    SELECT count(*) AS intentos_pago, count(*) FILTER (WHERE estado = 'RECHAZADO') AS pagos_rechazados
    FROM public.pagos WHERE compra_id = c.id
) p ON true
WHERE c.estado IN ('FALLIDA', 'CANCELADA', 'EXPIRADA');

CREATE OR REPLACE VIEW bi.vw_calificacion_presentador AS
SELECT (re.fecha_creacion AT TIME ZONE 'UTC')::date AS fecha,
       pr.id AS presentador_id, pr.nombre AS presentador_nombre,
       e.id AS evento_id, e.categoria_id, e.recinto_id, r.dueno_id, e.estado AS evento_estado,
       count(*) AS resenas_aprobadas, sum(re.calificacion) AS suma_calificaciones,
       avg(re.calificacion) AS calificacion_promedio
FROM public.resenas re JOIN public.evento_presentadores ep ON ep.evento_id = re.evento_id
JOIN public.presentadores pr ON pr.id = ep.presentador_id JOIN public.eventos e ON e.id = re.evento_id
JOIN public.recintos r ON r.id = e.recinto_id
WHERE re.estado = 'APROBADA'
GROUP BY (re.fecha_creacion AT TIME ZONE 'UTC')::date, pr.id, pr.nombre,
         e.id, e.categoria_id, e.recinto_id, r.dueno_id, e.estado;

CREATE OR REPLACE VIEW bi.vw_eventos_por_estado AS
SELECT e.id AS evento_id, e.nombre AS evento_nombre, e.estado AS evento_estado,
       e.fecha_inicio, (e.fecha_inicio AT TIME ZONE 'UTC')::date AS fecha,
       e.categoria_id, e.recinto_id, r.dueno_id
FROM public.eventos e JOIN public.recintos r ON r.id = e.recinto_id;

CREATE OR REPLACE VIEW bi.vw_inscritos_carreras AS
SELECT (b.fecha_emision AT TIME ZONE 'UTC')::date AS fecha,
       e.id AS evento_id, e.nombre AS evento_nombre, e.estado AS evento_estado,
       e.categoria_id, e.recinto_id, r.dueno_id, upper(btrim(t.nombre)) AS distancia,
       count(*) AS inscritos, count(*) FILTER (WHERE b.estado = 'USADO') AS participantes_validados
FROM public.boletos b JOIN public.detalle_compra d ON d.id = b.detalle_compra_id
JOIN public.tipos_entrada t ON t.id = d.tipo_entrada_id JOIN public.eventos e ON e.id = t.evento_id
JOIN public.categorias_evento cat ON cat.id = e.categoria_id JOIN public.recintos r ON r.id = e.recinto_id
WHERE lower(btrim(cat.nombre)) = 'carrera' AND upper(btrim(t.nombre)) IN ('5K', '10K', '21K', '42K')
  AND b.estado IN ('VALIDO', 'USADO')
GROUP BY (b.fecha_emision AT TIME ZONE 'UTC')::date, e.id, e.nombre, e.estado,
         e.categoria_id, e.recinto_id, r.dueno_id, upper(btrim(t.nombre));

CREATE OR REPLACE VIEW bi.vw_uso_agente_ia AS
SELECT (fecha_hora AT TIME ZONE 'UTC')::date AS fecha, modelo, estado,
       count(*) AS consultas, avg(tiempo_respuesta_ms) AS tiempo_promedio_ms,
       coalesce(sum(tokens_entrada), 0) AS tokens_entrada, coalesce(sum(tokens_salida), 0) AS tokens_salida
FROM public.logs_agente_ia
GROUP BY (fecha_hora AT TIME ZONE 'UTC')::date, modelo, estado;

CREATE OR REPLACE VIEW bi.vw_auditoria_operaciones AS
SELECT (fecha_hora AT TIME ZONE 'UTC')::date AS fecha, entidad_afectada, accion,
       count(*) AS operaciones, count(DISTINCT usuario_id) AS usuarios_identificados
FROM public.logs_auditoria GROUP BY (fecha_hora AT TIME ZONE 'UTC')::date, entidad_afectada, accion;

CREATE OR REPLACE VIEW bi.vw_notificaciones_estado AS
SELECT (fecha_creacion AT TIME ZONE 'UTC')::date AS fecha, tipo, estado, count(*) AS notificaciones
FROM public.notificaciones GROUP BY (fecha_creacion AT TIME ZONE 'UTC')::date, tipo, estado;

RESET ROLE;
GRANT USAGE ON SCHEMA bi TO bi_reader, promotora_app;
REVOKE CREATE ON SCHEMA bi FROM bi_reader, promotora_app;
REVOKE ALL ON ALL TABLES IN SCHEMA bi FROM PUBLIC, bi_reader, promotora_app;
GRANT SELECT ON bi.vw_ventas_por_evento, bi.vw_ventas_por_categoria, bi.vw_ventas_por_recinto,
    bi.vw_ventas_diarias, bi.vw_ocupacion_localidad, bi.vw_asistencia_evento,
    bi.vw_ingresos_dueno, bi.vw_comisiones_plataforma, bi.vw_top_clientes,
    bi.vw_compras_no_exitosas, bi.vw_calificacion_presentador, bi.vw_eventos_por_estado,
    bi.vw_inscritos_carreras, bi.vw_uso_agente_ia, bi.vw_auditoria_operaciones,
    bi.vw_notificaciones_estado TO bi_reader, promotora_app;
