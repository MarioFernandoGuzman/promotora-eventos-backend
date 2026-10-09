-- F1-07: indices, integridad y permisos; BASE_DE_DATOS.md §§6, 8 y 9.
-- Suposiciones aprobadas: localidades y tipos inactivos siguen contando;
-- un recinto con localidades exige capacidad_total no nula.
-- Las actualizaciones neutras de padres serializan escrituras relacionadas:
-- en READ COMMITTED la validacion ve lo confirmado; en REPEATABLE READ una
-- version obsoleta produce serialization_failure. Reintentar la transaccion
-- completa ante 40001/40P01; no reintentar solo la sentencia fallida.
-- No se modifica cupo_disponible, saldo ni estados automaticamente. La
-- conciliacion y los componentes del cobro corresponden a F7 y sus pruebas.
-- Roles de grupo NOLOGIN, sin contrasenas: las credenciales y membresias se
-- provisionan fuera del repositorio. flyway es propietario del DDL operativo;
-- promotora_app no recibe acceso al historial Flyway ni permisos de DDL.
-- Se reutilizan indices PK/UNIQUE cuando ya cubren el prefijo requerido.

CREATE INDEX idx_recintos_dueno ON recintos (dueno_id);
CREATE INDEX idx_eventos_recinto ON eventos (recinto_id);
CREATE INDEX idx_eventos_fecha_inicio ON eventos (fecha_inicio);
CREATE INDEX idx_eventos_estado ON eventos (estado);
CREATE INDEX idx_eventos_categoria ON eventos (categoria_id);
CREATE INDEX idx_eventos_creador ON eventos (creado_por);
CREATE INDEX idx_evento_presentadores_presentador ON evento_presentadores (presentador_id);
CREATE INDEX idx_tipos_entrada_evento ON tipos_entrada (evento_id);
CREATE INDEX idx_tipos_entrada_localidad ON tipos_entrada (localidad_id);
CREATE INDEX idx_compras_cliente ON compras (cliente_id);
CREATE INDEX idx_compras_fecha ON compras (fecha_compra);
CREATE INDEX idx_compras_estado ON compras (estado);
CREATE INDEX idx_compras_evento ON compras (evento_id);
CREATE INDEX idx_compras_expiracion ON compras (expira_en) WHERE estado = 'PENDIENTE';
CREATE INDEX idx_compras_procesando ON compras (procesando_desde) WHERE estado = 'PROCESANDO';
CREATE INDEX idx_detalle_compra_compra ON detalle_compra (compra_id);
CREATE INDEX idx_detalle_compra_tipo ON detalle_compra (tipo_entrada_id);
CREATE INDEX idx_boletos_detalle ON boletos (detalle_compra_id);
CREATE INDEX idx_boletos_estado ON boletos (estado);
CREATE INDEX idx_resenas_evento ON resenas (evento_id);
CREATE INDEX idx_logs_auditoria_fecha ON logs_auditoria (fecha_hora);
CREATE INDEX idx_logs_auditoria_usuario ON logs_auditoria (usuario_id);
CREATE INDEX idx_logs_auditoria_entidad ON logs_auditoria (entidad_afectada, entidad_id);
CREATE INDEX idx_logs_agente_fecha ON logs_agente_ia (fecha_hora);
CREATE INDEX idx_notificaciones_estado ON notificaciones (estado);
CREATE INDEX idx_notificaciones_usuario ON notificaciones (usuario_id);
CREATE INDEX idx_movimientos_cuenta_fecha ON movimientos_financieros (cuenta_id, fecha);
CREATE INDEX idx_pagos_compra ON pagos (compra_id);
CREATE INDEX idx_outbox_pendientes ON outbox_mensajes (id) WHERE publicado = false;
CREATE INDEX idx_refresh_usuario ON refresh_tokens (usuario_id);
CREATE INDEX idx_refresh_familia ON refresh_tokens (familia_id);
CREATE UNIQUE INDEX uq_cuentas_plataforma_moneda ON cuentas_financieras (moneda) WHERE tipo = 'PLATAFORMA';
CREATE UNIQUE INDEX uq_cuentas_impuestos_moneda ON cuentas_financieras (moneda) WHERE tipo = 'IMPUESTOS';

CREATE FUNCTION bloquear_padres_integridad() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
BEGIN
    IF TG_TABLE_NAME IN ('localidades', 'eventos') THEN
        UPDATE public.recintos SET capacidad_total = capacidad_total
        WHERE id IN (NEW.recinto_id, CASE WHEN TG_OP = 'UPDATE' THEN OLD.recinto_id END);
    ELSIF TG_TABLE_NAME = 'tipos_entrada' THEN
        UPDATE public.eventos SET estado = estado
        WHERE id IN (NEW.evento_id, CASE WHEN TG_OP = 'UPDATE' THEN OLD.evento_id END);
        UPDATE public.localidades SET activo = activo
        WHERE id IN (NEW.localidad_id, CASE WHEN TG_OP = 'UPDATE' THEN OLD.localidad_id END);
    ELSIF TG_TABLE_NAME = 'compras' THEN
        UPDATE public.eventos SET estado = estado
        WHERE id IN (NEW.evento_id, OLD.evento_id);
    ELSIF TG_TABLE_NAME = 'detalle_compra' THEN
        UPDATE public.compras SET estado = estado
        WHERE id IN (NEW.compra_id, CASE WHEN TG_OP = 'UPDATE' THEN OLD.compra_id END);
        UPDATE public.tipos_entrada SET cupo_disponible = cupo_disponible
        WHERE id IN (NEW.tipo_entrada_id, CASE WHEN TG_OP = 'UPDATE' THEN OLD.tipo_entrada_id END);
    END IF;
    RETURN NEW;
END;
$$;

CREATE FUNCTION validar_integridad_catalogo_ventas() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
DECLARE compra_validar integer;
BEGIN
    -- Camino frecuente: validar solo la compra afectada, sin recorrer catalogos.
    IF TG_TABLE_NAME IN ('detalle_compra', 'compras') THEN
        IF TG_TABLE_NAME = 'compras' THEN compra_validar := NEW.id;
        ELSE compra_validar := NEW.compra_id; END IF;
        IF EXISTS (
            SELECT 1 FROM public.detalle_compra d JOIN public.compras c ON c.id = d.compra_id
            JOIN public.tipos_entrada t ON t.id = d.tipo_entrada_id
            WHERE c.id = compra_validar AND c.evento_id <> t.evento_id
        ) THEN
            RAISE EXCEPTION 'El detalle pertenece a otro evento que la compra' USING ERRCODE = '23514';
        END IF;
        RETURN NULL;
    END IF;
    IF EXISTS (
        SELECT 1 FROM public.localidades l JOIN public.recintos r ON r.id = l.recinto_id
        GROUP BY r.id, r.capacidad_total
        HAVING r.capacidad_total IS NULL OR sum(l.capacidad::bigint) > r.capacidad_total
    ) THEN
        RAISE EXCEPTION 'Las localidades exceden la capacidad del recinto o falta capacidad_total' USING ERRCODE = '23514';
    END IF;
    IF EXISTS (
        SELECT 1 FROM public.tipos_entrada t
        JOIN public.eventos e ON e.id = t.evento_id
        JOIN public.localidades l ON l.id = t.localidad_id
        WHERE e.recinto_id <> l.recinto_id
    ) THEN
        RAISE EXCEPTION 'La localidad no pertenece al recinto del evento' USING ERRCODE = '23514';
    END IF;
    IF EXISTS (
        SELECT 1 FROM public.tipos_entrada t JOIN public.localidades l ON l.id = t.localidad_id
        GROUP BY t.evento_id, l.id, l.capacidad
        HAVING sum(t.cupo_total::bigint) > l.capacidad
    ) THEN
        RAISE EXCEPTION 'El cupo total por evento y localidad excede su capacidad' USING ERRCODE = '23514';
    END IF;
    IF EXISTS (
        SELECT 1 FROM public.detalle_compra d JOIN public.compras c ON c.id = d.compra_id
        JOIN public.tipos_entrada t ON t.id = d.tipo_entrada_id
        WHERE c.evento_id <> t.evento_id
    ) THEN
        RAISE EXCEPTION 'El detalle pertenece a otro evento que la compra' USING ERRCODE = '23514';
    END IF;
    RETURN NULL;
END;
$$;

CREATE TRIGGER bloquear_localidades_insert BEFORE INSERT ON localidades FOR EACH ROW EXECUTE FUNCTION bloquear_padres_integridad();
CREATE TRIGGER bloquear_localidades_update BEFORE UPDATE OF recinto_id, capacidad ON localidades FOR EACH ROW
WHEN (OLD.recinto_id IS DISTINCT FROM NEW.recinto_id OR OLD.capacidad IS DISTINCT FROM NEW.capacidad) EXECUTE FUNCTION bloquear_padres_integridad();
CREATE TRIGGER validar_localidades_insert AFTER INSERT ON localidades FOR EACH ROW EXECUTE FUNCTION validar_integridad_catalogo_ventas();
CREATE TRIGGER validar_localidades_update AFTER UPDATE OF recinto_id, capacidad ON localidades FOR EACH ROW
WHEN (OLD.recinto_id IS DISTINCT FROM NEW.recinto_id OR OLD.capacidad IS DISTINCT FROM NEW.capacidad) EXECUTE FUNCTION validar_integridad_catalogo_ventas();
CREATE TRIGGER validar_recintos_update AFTER UPDATE OF capacidad_total ON recintos FOR EACH ROW
WHEN (OLD.capacidad_total IS DISTINCT FROM NEW.capacidad_total) EXECUTE FUNCTION validar_integridad_catalogo_ventas();
CREATE TRIGGER bloquear_eventos_update BEFORE UPDATE OF recinto_id ON eventos FOR EACH ROW
WHEN (OLD.recinto_id IS DISTINCT FROM NEW.recinto_id) EXECUTE FUNCTION bloquear_padres_integridad();
CREATE TRIGGER validar_eventos_update AFTER UPDATE OF recinto_id ON eventos FOR EACH ROW
WHEN (OLD.recinto_id IS DISTINCT FROM NEW.recinto_id) EXECUTE FUNCTION validar_integridad_catalogo_ventas();
CREATE TRIGGER bloquear_tipos_insert BEFORE INSERT ON tipos_entrada FOR EACH ROW EXECUTE FUNCTION bloquear_padres_integridad();
CREATE TRIGGER bloquear_tipos_update BEFORE UPDATE OF evento_id, localidad_id, cupo_total ON tipos_entrada FOR EACH ROW
WHEN (OLD.evento_id IS DISTINCT FROM NEW.evento_id OR OLD.localidad_id IS DISTINCT FROM NEW.localidad_id OR OLD.cupo_total IS DISTINCT FROM NEW.cupo_total) EXECUTE FUNCTION bloquear_padres_integridad();
CREATE TRIGGER validar_tipos_insert AFTER INSERT ON tipos_entrada FOR EACH ROW EXECUTE FUNCTION validar_integridad_catalogo_ventas();
CREATE TRIGGER validar_tipos_update AFTER UPDATE OF evento_id, localidad_id, cupo_total ON tipos_entrada FOR EACH ROW
WHEN (OLD.evento_id IS DISTINCT FROM NEW.evento_id OR OLD.localidad_id IS DISTINCT FROM NEW.localidad_id OR OLD.cupo_total IS DISTINCT FROM NEW.cupo_total) EXECUTE FUNCTION validar_integridad_catalogo_ventas();
CREATE TRIGGER bloquear_compras_update BEFORE UPDATE OF evento_id ON compras FOR EACH ROW
WHEN (OLD.evento_id IS DISTINCT FROM NEW.evento_id) EXECUTE FUNCTION bloquear_padres_integridad();
CREATE TRIGGER validar_compras_update AFTER UPDATE OF evento_id ON compras FOR EACH ROW
WHEN (OLD.evento_id IS DISTINCT FROM NEW.evento_id) EXECUTE FUNCTION validar_integridad_catalogo_ventas();
CREATE TRIGGER bloquear_detalle_insert BEFORE INSERT ON detalle_compra FOR EACH ROW EXECUTE FUNCTION bloquear_padres_integridad();
CREATE TRIGGER bloquear_detalle_update BEFORE UPDATE OF compra_id, tipo_entrada_id ON detalle_compra FOR EACH ROW
WHEN (OLD.compra_id IS DISTINCT FROM NEW.compra_id OR OLD.tipo_entrada_id IS DISTINCT FROM NEW.tipo_entrada_id) EXECUTE FUNCTION bloquear_padres_integridad();
CREATE TRIGGER validar_detalle_insert AFTER INSERT ON detalle_compra FOR EACH ROW EXECUTE FUNCTION validar_integridad_catalogo_ventas();
CREATE TRIGGER validar_detalle_update AFTER UPDATE OF compra_id, tipo_entrada_id ON detalle_compra FOR EACH ROW
WHEN (OLD.compra_id IS DISTINCT FROM NEW.compra_id OR OLD.tipo_entrada_id IS DISTINCT FROM NEW.tipo_entrada_id) EXECUTE FUNCTION validar_integridad_catalogo_ventas();

CREATE FUNCTION bloquear_usuario_perfil() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
BEGIN
    IF TG_OP <> 'INSERT' THEN
        UPDATE public.usuarios SET estado = estado WHERE id = OLD.usuario_id;
    END IF;
    IF TG_OP <> 'DELETE' THEN
        UPDATE public.usuarios SET estado = estado WHERE id = NEW.usuario_id;
        RETURN NEW;
    END IF;
    RETURN OLD;
END;
$$;

CREATE FUNCTION validar_perfil_usuario() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
DECLARE
    usuario_actual integer;
    rol_actual text;
    perfiles integer;
    rol_perfil text;
    usuarios_validar integer[];
BEGIN
    IF TG_TABLE_NAME = 'usuarios' THEN
        usuarios_validar := ARRAY[NEW.id];
    ELSIF TG_OP = 'INSERT' THEN
        usuarios_validar := ARRAY[NEW.usuario_id];
    ELSIF TG_OP = 'DELETE' THEN
        usuarios_validar := ARRAY[OLD.usuario_id];
    ELSE
        usuarios_validar := ARRAY[OLD.usuario_id, NEW.usuario_id];
    END IF;
    FOR usuario_actual IN
        SELECT DISTINCT x FROM unnest(usuarios_validar) AS ids(x)
    LOOP
        SELECT rol INTO rol_actual FROM public.usuarios WHERE id = usuario_actual;
        IF NOT FOUND THEN CONTINUE; END IF;
        SELECT count(*), min(p.rol) INTO perfiles, rol_perfil FROM (
            SELECT 'CLIENTE' AS rol FROM public.clientes WHERE usuario_id = usuario_actual
            UNION ALL SELECT 'ADMINISTRADOR' FROM public.administradores WHERE usuario_id = usuario_actual
            UNION ALL SELECT 'DUENO_RECINTO' FROM public.duenos_recinto WHERE usuario_id = usuario_actual
        ) p;
        IF perfiles <> 1 OR rol_perfil <> rol_actual THEN
            RAISE EXCEPTION 'Usuario % requiere exactamente un perfil acorde al rol %', usuario_actual, rol_actual USING ERRCODE = '23514';
        END IF;
    END LOOP;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER validar_usuario_perfil AFTER INSERT OR UPDATE OF rol ON usuarios
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validar_perfil_usuario();
DO $$
DECLARE tabla text;
BEGIN
    FOREACH tabla IN ARRAY ARRAY['clientes', 'administradores', 'duenos_recinto'] LOOP
        EXECUTE format('CREATE TRIGGER bloquear_perfil BEFORE INSERT OR UPDATE OR DELETE ON public.%I FOR EACH ROW EXECUTE FUNCTION public.bloquear_usuario_perfil()', tabla);
        EXECUTE format('CREATE CONSTRAINT TRIGGER validar_perfil AFTER INSERT OR UPDATE OR DELETE ON public.%I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.validar_perfil_usuario()', tabla);
    END LOOP;
END;
$$;

CREATE FUNCTION rechazar_mutacion_inmutable() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION '% solo admite INSERT: operacion % rechazada', TG_TABLE_NAME, TG_OP USING ERRCODE = '23514';
END;
$$;
DO $$
DECLARE tabla text;
BEGIN
    FOREACH tabla IN ARRAY ARRAY['movimientos_financieros', 'logs_auditoria', 'logs_agente_ia'] LOOP
        EXECUTE format('CREATE TRIGGER proteger_inmutabilidad BEFORE UPDATE OR DELETE OR TRUNCATE ON public.%I FOR EACH STATEMENT EXECUTE FUNCTION public.rechazar_mutacion_inmutable()', tabla);
    END LOOP;
END;
$$;

CREATE FUNCTION validar_cuenta_movimiento() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
DECLARE estado_cuenta text;
BEGIN
    UPDATE public.cuentas_financieras SET saldo = saldo WHERE id = NEW.cuenta_id
    RETURNING estado INTO estado_cuenta;
    IF estado_cuenta = 'CERRADA' THEN
        RAISE EXCEPTION 'No se admiten movimientos en cuentas cerradas' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER validar_movimiento_cuenta BEFORE INSERT ON movimientos_financieros FOR EACH ROW EXECUTE FUNCTION validar_cuenta_movimiento();

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'flyway') THEN
        CREATE ROLE flyway NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'promotora_app') THEN
        CREATE ROLE promotora_app NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;
END;
$$;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO flyway;
GRANT USAGE ON SCHEMA public TO promotora_app;
DO $$ BEGIN
    EXECUTE format('GRANT CREATE ON DATABASE %I TO flyway', current_database());
END; $$;

DO $$
DECLARE objeto record;
BEGIN
    FOR objeto IN SELECT tablename FROM pg_tables WHERE schemaname = 'public'
        AND tablename = ANY (ARRAY['usuarios','clientes','administradores','duenos_recinto','refresh_tokens',
            'recintos','localidades','categorias_evento','presentadores','eventos','evento_presentadores','tipos_entrada',
            'compras','detalle_compra','boletos','pagos','cuentas_financieras','movimientos_financieros','resenas',
            'documentos_rag','chunks_rag','logs_auditoria','logs_agente_ia','notificaciones','outbox_mensajes','mensajes_procesados']) LOOP
        EXECUTE format('ALTER TABLE public.%I OWNER TO flyway', objeto.tablename);
        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM PUBLIC, promotora_app', objeto.tablename);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.%I TO promotora_app', objeto.tablename);
    END LOOP;
    FOR objeto IN SELECT p.oid::regprocedure AS firma FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'public' AND p.proname IN ('bloquear_padres_integridad', 'validar_integridad_catalogo_ventas', 'bloquear_usuario_perfil', 'validar_perfil_usuario', 'rechazar_mutacion_inmutable', 'validar_cuenta_movimiento') LOOP
        EXECUTE format('ALTER FUNCTION %s OWNER TO flyway', objeto.firma);
        EXECUTE format('REVOKE ALL ON FUNCTION %s FROM PUBLIC', objeto.firma);
        EXECUTE format('GRANT EXECUTE ON FUNCTION %s TO promotora_app', objeto.firma);
    END LOOP;
END;
$$;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO promotora_app;
-- Flyway mantiene un bloqueo del historial en otra conexion mientras migra;
-- conceder DML no requiere transferir su propiedad durante ese bloqueo.
GRANT SELECT, INSERT, UPDATE, DELETE ON flyway_schema_history TO flyway;
REVOKE DELETE ON usuarios, compras, detalle_compra, boletos, pagos, cuentas_financieras FROM promotora_app;
REVOKE UPDATE, DELETE ON movimientos_financieros, logs_auditoria, logs_agente_ia FROM promotora_app;

