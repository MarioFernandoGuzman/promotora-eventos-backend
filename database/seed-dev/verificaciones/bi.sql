\set ON_ERROR_STOP on
BEGIN;
SET TIME ZONE 'Pacific/Honolulu';
CREATE FUNCTION pg_temp.exige(condicion boolean, mensaje text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN IF condicion IS DISTINCT FROM true THEN RAISE EXCEPTION '%', mensaje; END IF; END; $$;
INSERT INTO usuarios(email,password_hash,nombre,apellido,rol) VALUES ('bi-owner@example.invalid','fixture-no-credencial','D','D','DUENO_RECINTO') RETURNING id AS dueno_usuario \gset
INSERT INTO duenos_recinto(usuario_id,empresa) VALUES (:dueno_usuario,'BI') RETURNING id AS dueno \gset
INSERT INTO usuarios(email,password_hash,nombre,apellido,rol) VALUES ('bi-client@example.invalid','fixture-no-credencial','C','C','CLIENTE') RETURNING id AS cliente_usuario \gset
INSERT INTO clientes(usuario_id) VALUES (:cliente_usuario) RETURNING id AS cliente \gset
INSERT INTO usuarios(email,password_hash,nombre,apellido,rol) VALUES ('bi-client2@example.invalid','fixture-no-credencial','C2','C2','CLIENTE') RETURNING id AS cliente2_usuario \gset
INSERT INTO clientes(usuario_id) VALUES (:cliente2_usuario) RETURNING id AS cliente2 \gset
INSERT INTO recintos(dueno_id,nombre,capacidad_total) VALUES (:dueno,'BI',100) RETURNING id AS recinto \gset
INSERT INTO localidades(recinto_id,nombre,capacidad) VALUES (:recinto,'L',100) RETURNING id AS localidad \gset
SELECT id AS concierto FROM categorias_evento WHERE nombre='Concierto' \gset
SELECT id AS carrera FROM categorias_evento WHERE nombre='Carrera' \gset
INSERT INTO eventos(recinto_id,categoria_id,creado_por,nombre,fecha_inicio,estado) VALUES (:recinto,:concierto,:dueno_usuario,'BI-Concierto','2026-01-02 00:30+00','EN_VENTA') RETURNING id AS e1 \gset
INSERT INTO eventos(recinto_id,categoria_id,creado_por,nombre,fecha_inicio) VALUES (:recinto,:carrera,:dueno_usuario,'BI-Carrera','2026-01-02 00:30+00') RETURNING id AS e2 \gset
INSERT INTO tipos_entrada(evento_id,localidad_id,nombre,precio,cupo_total,cupo_disponible) VALUES (:e1,:localidad,'General',10,60,55) RETURNING id AS t1 \gset
INSERT INTO tipos_entrada(evento_id,localidad_id,nombre,precio,cupo_total,cupo_disponible) VALUES (:e1,:localidad,'Preferente',10,40,38) RETURNING id AS t2 \gset
INSERT INTO tipos_entrada(evento_id,nombre,precio,cupo_total,cupo_disponible) VALUES (:e2,'5k',10,100,98) RETURNING id AS t5k \gset
INSERT INTO compras(cliente_id,evento_id,fecha_compra,estado,expira_en,idempotency_key,subtotal,impuestos,comision_plataforma,total)
VALUES (:cliente,:e1,'2026-01-02 00:30+00','COMPLETADA',now(),'bi-1',30,3,2,35) RETURNING id AS c1 \gset
INSERT INTO detalle_compra(compra_id,tipo_entrada_id,cantidad,precio_unitario,subtotal) VALUES (:c1,:t1,1,10,10) RETURNING id AS d1 \gset
INSERT INTO detalle_compra(compra_id,tipo_entrada_id,cantidad,precio_unitario,subtotal) VALUES (:c1,:t2,2,10,20) RETURNING id AS d2 \gset
INSERT INTO compras(cliente_id,evento_id,fecha_compra,estado,expira_en,idempotency_key,moneda,subtotal,impuestos,comision_plataforma,total)
VALUES (:cliente,:e1,'2026-01-02 00:30+00','COMPLETADA',now(),'bi-usd','USD',20,2,1,23) RETURNING id AS cusd \gset
INSERT INTO detalle_compra(compra_id,tipo_entrada_id,cantidad,precio_unitario,subtotal) VALUES (:cusd,:t1,2,10,20);
INSERT INTO compras(cliente_id,evento_id,fecha_compra,estado,expira_en,idempotency_key) VALUES (:cliente,:e1,'2026-01-02 00:30+00','FALLIDA',now(),'bi-fallida') RETURNING id AS cf \gset
INSERT INTO compras(cliente_id,evento_id,fecha_compra,estado,expira_en,idempotency_key,subtotal,total)
VALUES (:cliente,:e2,'2026-01-02 00:30+00','COMPLETADA',now(),'bi-carrera',20,20) RETURNING id AS cr \gset
INSERT INTO detalle_compra(compra_id,tipo_entrada_id,cantidad,precio_unitario,subtotal) VALUES (:cr,:t5k,2,10,20) RETURNING id AS dr \gset
INSERT INTO pagos(compra_id,pasarela,metodo,monto,estado,fecha) VALUES
(:c1,'SIMULADA','TRANSFERENCIA',35,'RECHAZADO','2026-01-01 23:00+00'),
(:c1,'SIMULADA','TARJETA',35,'APROBADO','2026-01-02 00:00+00'),
(:c1,'SIMULADA','TARJETA',35,'APROBADO','2026-01-02 01:00+00'),
(:cf,'SIMULADA','TARJETA',35,'RECHAZADO',now()),(:cf,'SIMULADA','TARJETA',35,'RECHAZADO',now());
INSERT INTO boletos(detalle_compra_id,codigo_qr,estado,fecha_emision) VALUES
(:d1,'fixture1','USADO','2026-01-02 00:30+00'),(:d2,'fixture2','VALIDO','2026-01-02 00:30+00'),
(:d2,'fixture3','ANULADO','2026-01-02 00:30+00'),(:dr,'fixture4','USADO','2026-01-02 00:30+00'),
(:dr,'fixture5','VALIDO','2026-01-02 00:30+00');
INSERT INTO cuentas_financieras(tipo,dueno_id) VALUES ('DUENO',:dueno) RETURNING id AS cuenta_dueno \gset
SELECT id AS cuenta_plataforma FROM cuentas_financieras WHERE tipo='PLATAFORMA' AND moneda='GTQ' \gset
SELECT id AS cuenta_impuestos FROM cuentas_financieras WHERE tipo='IMPUESTOS' AND moneda='GTQ' \gset
INSERT INTO movimientos_financieros(cuenta_id,compra_id,tipo,monto,fecha) VALUES
(:cuenta_dueno,:c1,'VENTA',30,'2026-01-02 00:30+00'),(:cuenta_plataforma,:c1,'COMISION',2,'2026-01-02 00:30+00'),
(:cuenta_impuestos,:c1,'IMPUESTO',3,'2026-01-02 00:30+00'),
(:cuenta_dueno,:c1,'REEMBOLSO',-30,'2026-01-02 00:30+00'),(:cuenta_plataforma,:c1,'REEMBOLSO',-2,'2026-01-02 00:30+00'),
(:cuenta_impuestos,:c1,'REEMBOLSO',-3,'2026-01-02 00:30+00');
INSERT INTO presentadores(nombre,tipo) VALUES ('P1','ARTISTA') RETURNING id AS p1 \gset
INSERT INTO presentadores(nombre,tipo) VALUES ('P2','ARTISTA') RETURNING id AS p2 \gset
INSERT INTO evento_presentadores(evento_id,presentador_id) VALUES (:e1,:p1),(:e1,:p2);
INSERT INTO resenas(evento_id,cliente_id,calificacion,estado,fecha_creacion) VALUES (:e1,:cliente,4,'APROBADA','2026-01-02 00:30+00'),(:e1,:cliente2,1,'PENDIENTE','2026-01-02 00:30+00');
INSERT INTO logs_agente_ia(consulta,session_id,modelo,tokens_entrada,tokens_salida,tiempo_respuesta_ms,estado,fecha_hora) VALUES
('fixture',gen_random_uuid(),'fixture',10,5,100,'OK','2026-01-02 00:30+00'),
('fixture',gen_random_uuid(),'fixture',NULL,NULL,NULL,'ERROR','2026-01-02 00:30+00');
INSERT INTO logs_auditoria(entidad_afectada,accion,usuario_id,fecha_hora) VALUES ('fixture','CREATE',:cliente_usuario,'2026-01-02 00:30+00');
INSERT INTO notificaciones(tipo,estado,fecha_creacion) VALUES ('fixture','ENVIADO','2026-01-02 00:30+00'),('fixture','FALLIDO','2026-01-02 00:30+00');
SET CONSTRAINTS ALL IMMEDIATE;
SELECT pg_temp.exige((SELECT total=35 AND boletos_vendidos=3 AND compras_completadas=1 AND fecha='2026-01-02' FROM bi.vw_ventas_por_evento WHERE evento_id=:e1 AND moneda='GTQ'),'R01: dinero duplicado o fecha sin UTC');
SELECT pg_temp.exige((SELECT total=23 FROM bi.vw_ventas_por_evento WHERE evento_id=:e1 AND moneda='USD'),'R01: monedas mezcladas');
SELECT pg_temp.exige((SELECT sum(total)=35 FROM bi.vw_ventas_por_categoria WHERE categoria_id=:concierto AND moneda='GTQ'),'R02: categoria incorrecta');
SELECT pg_temp.exige((SELECT sum(total)=55 FROM bi.vw_ventas_por_recinto WHERE recinto_id=:recinto AND moneda='GTQ'),'R03: recinto incorrecto');
SELECT pg_temp.exige((SELECT total=35 AND compras_completadas=1 FROM bi.vw_ventas_diarias WHERE evento_id=:e1 AND moneda='GTQ' AND metodo='TARJETA'),'R04: pagos multiplicaron ventas');
SELECT pg_temp.exige((SELECT cupo_total=100 AND cupo_disponible=93 AND cupo_ocupado=7 AND porcentaje_ocupacion=7 FROM bi.vw_ocupacion_localidad WHERE evento_id=:e1),'R05: ocupacion incorrecta');
SELECT pg_temp.exige((SELECT boletos_emitidos=3 AND boletos_vendidos=2 AND boletos_usados=1 AND boletos_anulados=1 FROM bi.vw_asistencia_evento WHERE evento_id=:e1),'R06: anulados o usados mal contados');
SELECT pg_temp.exige((SELECT ventas=30 AND reembolsos=-30 AND movimiento_neto=0 FROM bi.vw_ingresos_dueno WHERE dueno_id=:dueno),'R07: signos financieros incorrectos');
SELECT pg_temp.exige((SELECT comisiones=2 AND reembolsos=-2 AND comision_neta=0 FROM bi.vw_comisiones_plataforma WHERE evento_id=:e1),'R08: comision neta incorrecta');
SELECT pg_temp.exige((SELECT sum(total)=55 FROM bi.vw_top_clientes WHERE cliente_id=:cliente AND moneda='GTQ'),'R09: top clientes duplicado');
SELECT pg_temp.exige((SELECT intentos_pago=2 AND pagos_rechazados=2 FROM bi.vw_compras_no_exitosas WHERE compra_estado='FALLIDA'),'R10: intentos duplican compras');
SELECT pg_temp.exige((SELECT count(*)=2 AND min(calificacion_promedio)=4 AND max(resenas_aprobadas)=1 FROM bi.vw_calificacion_presentador),'R11: incluye pendientes o duplica presentadores');
SELECT pg_temp.exige((SELECT count(*)=2 FROM bi.vw_eventos_por_estado),'R12: eventos incorrectos');
SELECT pg_temp.exige((SELECT distancia='5K' AND inscritos=2 AND participantes_validados=1 FROM bi.vw_inscritos_carreras),'R13: inscritos incorrectos');
SELECT pg_temp.exige((SELECT consultas=1 AND tokens_entrada=10 AND tokens_salida=5 AND tiempo_promedio_ms=100 FROM bi.vw_uso_agente_ia WHERE estado='OK'),'R14: uso de IA incorrecto');
SELECT pg_temp.exige((SELECT tokens_entrada=0 AND tiempo_promedio_ms IS NULL FROM bi.vw_uso_agente_ia WHERE estado='ERROR'),'R14: nulos incorrectos');
SELECT pg_temp.exige((SELECT operaciones=1 FROM bi.vw_auditoria_operaciones WHERE accion='CREATE'),'R15: auditoria incorrecta');
SELECT pg_temp.exige((SELECT sum(notificaciones)=2 FROM bi.vw_notificaciones_estado),'R15: notificaciones incorrectas');
SET ROLE bi_reader;
DO $$ DECLARE objeto record; BEGIN
    FOR objeto IN SELECT viewname FROM pg_views WHERE schemaname='bi' LOOP
        EXECUTE format('SELECT count(*) FROM bi.%I',objeto.viewname);
        IF has_table_privilege('bi_reader','bi.'||objeto.viewname,'INSERT,UPDATE,DELETE,TRUNCATE') THEN RAISE EXCEPTION 'BI tiene DML'; END IF;
    END LOOP;
    FOR objeto IN SELECT tablename FROM pg_tables WHERE schemaname='public' LOOP
        IF has_table_privilege('bi_reader','public.'||objeto.tablename,'SELECT,INSERT,UPDATE,DELETE') THEN RAISE EXCEPTION 'BI accede a tablas operativas'; END IF;
    END LOOP;
    BEGIN PERFORM count(*) FROM public.usuarios; RAISE EXCEPTION 'BI leyo usuarios'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
    IF has_schema_privilege('bi_reader','bi','CREATE') THEN RAISE EXCEPTION 'BI tiene CREATE'; END IF;
END; $$;
RESET ROLE;
SET ROLE promotora_app;
SELECT count(*) FROM bi.vw_ventas_por_evento;
RESET ROLE;
SELECT pg_temp.exige((SELECT count(*)=16 FROM pg_views WHERE schemaname='bi'),'Conteo de vistas distinto de 16');
ROLLBACK;
\echo 'F1-08: 15 reportes / 16 vistas, UTC, monedas, reintentos, reembolsos y permisos BI verificados.'
