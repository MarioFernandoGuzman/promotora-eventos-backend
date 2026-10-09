\set ON_ERROR_STOP on
BEGIN;
CREATE FUNCTION pg_temp.rechaza(sql text, codigo text DEFAULT '23514') RETURNS void LANGUAGE plpgsql AS $$
DECLARE recibido text;
BEGIN
    BEGIN
        EXECUTE sql;
        SET CONSTRAINTS ALL IMMEDIATE;
    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS recibido = RETURNED_SQLSTATE;
        IF recibido = codigo THEN RETURN; END IF;
        RAISE EXCEPTION 'SQLSTATE esperado %, recibido %: %', codigo, recibido, SQLERRM;
    END;
    RAISE EXCEPTION 'Se acepto una operacion invalida: %', sql;
END;
$$;
INSERT INTO usuarios(email,password_hash,nombre,apellido,rol) VALUES ('dueno@example.invalid','fixture-no-credencial','D','D','DUENO_RECINTO') RETURNING id AS dueno_usuario \gset
INSERT INTO duenos_recinto(usuario_id,empresa) VALUES (:dueno_usuario,'Fixture') RETURNING id AS dueno \gset
INSERT INTO usuarios(email,password_hash,nombre,apellido,rol) VALUES ('cliente@example.invalid','fixture-no-credencial','C','C','CLIENTE') RETURNING id AS cliente_usuario \gset
INSERT INTO clientes(usuario_id) VALUES (:cliente_usuario) RETURNING id AS cliente \gset
SET CONSTRAINTS ALL IMMEDIATE;
SET CONSTRAINTS ALL DEFERRED;
SELECT pg_temp.rechaza('INSERT INTO usuarios(email,password_hash,nombre,apellido,rol) VALUES (''sinperfil@example.invalid'',''fixture'',''X'',''X'',''CLIENTE'')');
SELECT pg_temp.rechaza(format('INSERT INTO administradores(usuario_id) VALUES (%s)', :cliente_usuario));
SELECT pg_temp.rechaza(format('UPDATE usuarios SET rol=''ADMINISTRADOR'' WHERE id=%s', :cliente_usuario));
SELECT pg_temp.rechaza(format('DELETE FROM clientes WHERE id=%s', :cliente));
-- Cambio de rol valido en una transaccion, aunque intermedio tenga dos perfiles.
INSERT INTO administradores(usuario_id) VALUES (:cliente_usuario);
DELETE FROM clientes WHERE id=:cliente;
UPDATE usuarios SET rol='ADMINISTRADOR' WHERE id=:cliente_usuario;
SET CONSTRAINTS ALL IMMEDIATE;
SET CONSTRAINTS ALL DEFERRED;
INSERT INTO clientes(usuario_id) VALUES (:cliente_usuario) RETURNING id AS cliente \gset
DELETE FROM administradores WHERE usuario_id=:cliente_usuario;
UPDATE usuarios SET rol='CLIENTE' WHERE id=:cliente_usuario;
SET CONSTRAINTS ALL IMMEDIATE;
SET CONSTRAINTS ALL DEFERRED;
INSERT INTO recintos(dueno_id,nombre,capacidad_total) VALUES (:dueno,'R1',100) RETURNING id AS r1 \gset
INSERT INTO recintos(dueno_id,nombre,capacidad_total) VALUES (:dueno,'R2',100) RETURNING id AS r2 \gset
INSERT INTO recintos(dueno_id,nombre) VALUES (:dueno,'Sin capacidad') RETURNING id AS rn \gset
INSERT INTO localidades(recinto_id,nombre,capacidad,activo) VALUES (:r1,'L1',60,false) RETURNING id AS l1 \gset
INSERT INTO localidades(recinto_id,nombre,capacidad) VALUES (:r2,'L2',60) RETURNING id AS l2 \gset
SELECT pg_temp.rechaza(format('INSERT INTO localidades(recinto_id,nombre,capacidad) VALUES (%s,''exceso'',41)', :r1));
SELECT pg_temp.rechaza(format('INSERT INTO localidades(recinto_id,nombre,capacidad) VALUES (%s,''sin limite'',1)', :rn));
SELECT pg_temp.rechaza(format('UPDATE recintos SET capacidad_total=59 WHERE id=%s', :r1));
SELECT pg_temp.rechaza(format('UPDATE recintos SET capacidad_total=NULL WHERE id=%s', :r1));
INSERT INTO categorias_evento(nombre) VALUES ('Fixture') RETURNING id AS categoria \gset
INSERT INTO eventos(recinto_id,categoria_id,creado_por,nombre,fecha_inicio) VALUES (:r1,:categoria,:dueno_usuario,'E1',now()) RETURNING id AS e1 \gset
INSERT INTO eventos(recinto_id,categoria_id,creado_por,nombre,fecha_inicio) VALUES (:r2,:categoria,:dueno_usuario,'E2',now()) RETURNING id AS e2 \gset
INSERT INTO tipos_entrada(evento_id,localidad_id,nombre,precio,cupo_total,cupo_disponible,activo) VALUES (:e1,:l1,'T1',10,40,40,false) RETURNING id AS t1 \gset
INSERT INTO tipos_entrada(evento_id,localidad_id,nombre,precio,cupo_total,cupo_disponible) VALUES (:e2,:l2,'T2',10,40,40) RETURNING id AS t2 \gset
SELECT pg_temp.rechaza(format('INSERT INTO tipos_entrada(evento_id,localidad_id,nombre,precio,cupo_total,cupo_disponible) VALUES (%s,%s,''exceso'',10,21,21)', :e1,:l1));
SELECT pg_temp.rechaza(format('UPDATE localidades SET capacidad=39 WHERE id=%s', :l1));
SELECT pg_temp.rechaza(format('UPDATE localidades SET recinto_id=%s WHERE id=%s', :r2,:l1));
SELECT pg_temp.rechaza(format('UPDATE eventos SET recinto_id=%s WHERE id=%s', :r2,:e1));
SELECT pg_temp.rechaza(format('UPDATE tipos_entrada SET localidad_id=%s WHERE id=%s', :l2,:t1));
INSERT INTO compras(cliente_id,evento_id,expira_en,idempotency_key) VALUES (:cliente,:e1,now()+interval '10 minutes','fixture') RETURNING id AS compra \gset
INSERT INTO detalle_compra(compra_id,tipo_entrada_id,cantidad,precio_unitario,subtotal) VALUES (:compra,:t1,1,10,10) RETURNING id AS detalle \gset
SELECT pg_temp.rechaza(format('INSERT INTO detalle_compra(compra_id,tipo_entrada_id,cantidad,precio_unitario,subtotal) VALUES (%s,%s,1,10,10)', :compra,:t2));
SELECT pg_temp.rechaza(format('UPDATE compras SET evento_id=%s WHERE id=%s', :e2,:compra));
-- Tipo general sin localidad: reasignacion tambien se protege por detalle.
INSERT INTO tipos_entrada(evento_id,nombre,precio,cupo_total,cupo_disponible) VALUES (:e1,'general',10,10,10) RETURNING id AS tg \gset
INSERT INTO detalle_compra(compra_id,tipo_entrada_id,cantidad,precio_unitario,subtotal) VALUES (:compra,:tg,1,10,10);
SELECT pg_temp.rechaza(format('UPDATE tipos_entrada SET evento_id=%s WHERE id=%s', :e2,:tg));
INSERT INTO cuentas_financieras(tipo,moneda) VALUES ('PLATAFORMA','TST') RETURNING id AS plataforma \gset
INSERT INTO cuentas_financieras(tipo,moneda) VALUES ('IMPUESTOS','TST');
SELECT pg_temp.rechaza('INSERT INTO cuentas_financieras(tipo,moneda) VALUES (''PLATAFORMA'',''TST'')','23505');
SELECT pg_temp.rechaza('INSERT INTO cuentas_financieras(tipo,moneda) VALUES (''IMPUESTOS'',''TST'')','23505');
INSERT INTO cuentas_financieras(tipo,moneda) VALUES ('PLATAFORMA','USD'),('IMPUESTOS','USD');
INSERT INTO cuentas_financieras(tipo,dueno_id,estado,fecha_cierre) VALUES ('DUENO',:dueno,'CERRADA',now()) RETURNING id AS cerrada \gset
SELECT pg_temp.rechaza(format('INSERT INTO movimientos_financieros(cuenta_id,tipo,monto) VALUES (%s,''VENTA'',1)', :cerrada));
SELECT pg_temp.rechaza(format('UPDATE cuentas_financieras SET estado=''CERRADA'' WHERE id=%s', :plataforma));
INSERT INTO movimientos_financieros(cuenta_id,tipo,monto) VALUES (:plataforma,'COMISION',1);
UPDATE cuentas_financieras SET saldo=saldo+1 WHERE id=:plataforma;
INSERT INTO logs_auditoria(entidad_afectada,accion) VALUES ('fixture','CREATE');
INSERT INTO logs_agente_ia(consulta,session_id,estado) VALUES ('fixture',gen_random_uuid(),'OK');
SELECT pg_temp.rechaza('UPDATE movimientos_financieros SET monto=2');
SELECT pg_temp.rechaza('DELETE FROM movimientos_financieros');
SELECT pg_temp.rechaza('UPDATE logs_auditoria SET accion=''UPDATE''');
SELECT pg_temp.rechaza('DELETE FROM logs_auditoria');
SELECT pg_temp.rechaza('UPDATE logs_agente_ia SET estado=''ERROR''');
SELECT pg_temp.rechaza('DELETE FROM logs_agente_ia');
DO $$
DECLARE tabla text;
BEGIN
    FOREACH tabla IN ARRAY ARRAY['usuarios','compras','detalle_compra','boletos','pagos','cuentas_financieras','movimientos_financieros','logs_auditoria','logs_agente_ia'] LOOP
        IF has_table_privilege('promotora_app', 'public.'||tabla, 'DELETE') THEN RAISE EXCEPTION 'DELETE concedido: %', tabla; END IF;
    END LOOP;
    FOREACH tabla IN ARRAY ARRAY['movimientos_financieros','logs_auditoria','logs_agente_ia'] LOOP
        IF has_table_privilege('promotora_app', 'public.'||tabla, 'UPDATE') THEN RAISE EXCEPTION 'UPDATE concedido: %', tabla; END IF;
    END LOOP;
    IF has_schema_privilege('promotora_app','public','CREATE') OR has_table_privilege('promotora_app','flyway_schema_history','SELECT') THEN RAISE EXCEPTION 'Permisos indebidos'; END IF;
END;
$$;
SET ROLE promotora_app;
SELECT pg_temp.rechaza('DELETE FROM usuarios','42501');
SELECT pg_temp.rechaza('DELETE FROM compras','42501');
SELECT pg_temp.rechaza('UPDATE movimientos_financieros SET monto=2','42501');
SELECT pg_temp.rechaza('DELETE FROM logs_auditoria','42501');
INSERT INTO usuarios(email,password_hash,nombre,apellido,rol) VALUES ('app@example.invalid','fixture','A','A','CLIENTE') RETURNING id AS app_usuario \gset
INSERT INTO clientes(usuario_id) VALUES (:app_usuario);
INSERT INTO localidades(recinto_id,nombre,capacidad) VALUES (:r1,'App',10);
INSERT INTO tipos_entrada(evento_id,localidad_id,nombre,precio,cupo_total,cupo_disponible) VALUES (:e1,:l1,'App',1,10,10);
INSERT INTO movimientos_financieros(cuenta_id,tipo,monto) VALUES (:plataforma,'COMISION',1);
UPDATE cuentas_financieras SET saldo=saldo+1 WHERE id=:plataforma;
SET CONSTRAINTS ALL IMMEDIATE;
RESET ROLE;
SET ROLE flyway;
COMMENT ON TABLE usuarios IS 'Prueba DDL reversible con rol flyway';
SELECT count(*) FROM flyway_schema_history;
RESET ROLE;
ROLLBACK;
\echo 'F1-07: integridad, perfiles diferidos, cambios de padres, inmutabilidad y rol real verificados.'
