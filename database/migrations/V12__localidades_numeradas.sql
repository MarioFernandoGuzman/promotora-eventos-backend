-- EXT-NUM-02. Diseño aprobado: docs/LOCALIDADES_NUMERADAS.md.
-- No modifica migraciones anteriores ni convierte compras existentes.
-- Suposiciones concretadas: IDs BIGINT identity ALWAYS; coordenadas NUMERIC(10,6)
-- normalizadas, etiquetas sensibles a mayúsculas; FK históricas RESTRICT.
-- Cupos numerados son contadores conciliados al COMMIT, no inventario independiente.
-- Una mesa comparte tipo/precio. No se añaden tarifas, retenciones previas ni colas.
-- Distribución publicada inmutable; el borrador solo permite posiciones libres/bloqueadas.
-- El trigger diferido también protege escrituras antiguas que ignoren las asignaciones.

CREATE TABLE distribuciones_evento (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    evento_id INT NOT NULL REFERENCES eventos(id) ON DELETE RESTRICT,
    localidad_id INT NOT NULL REFERENCES localidades(id) ON DELETE RESTRICT,
    estado VARCHAR(20) NOT NULL DEFAULT 'BORRADOR',
    imagen_plano_url VARCHAR(255),
    fecha_creacion TIMESTAMPTZ NOT NULL DEFAULT now(),
    fecha_publicacion TIMESTAMPTZ,
    CONSTRAINT uq_distribuciones_evento_localidad UNIQUE(evento_id, localidad_id),
    CONSTRAINT ck_distribuciones_estado CHECK (
        (estado='BORRADOR' AND fecha_publicacion IS NULL) OR
        (estado='PUBLICADA' AND fecha_publicacion IS NOT NULL AND fecha_publicacion>=fecha_creacion))
);

CREATE TABLE mesas_evento (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    distribucion_id BIGINT NOT NULL REFERENCES distribuciones_evento(id) ON DELETE RESTRICT,
    etiqueta VARCHAR(50) NOT NULL CHECK (btrim(etiqueta)<>''),
    modalidad VARCHAR(20) NOT NULL CHECK (modalidad IN ('COMPLETA','POR_SILLA')),
    tipo_entrada_id INT NOT NULL REFERENCES tipos_entrada(id) ON DELETE RESTRICT,
    x NUMERIC(10,6) NOT NULL CHECK (x BETWEEN 0 AND 1),
    y NUMERIC(10,6) NOT NULL CHECK (y BETWEEN 0 AND 1),
    CONSTRAINT uq_mesas_distribucion_etiqueta UNIQUE(distribucion_id, etiqueta),
    CONSTRAINT uq_mesas_relacion UNIQUE(id, distribucion_id, tipo_entrada_id)
);

CREATE TABLE asientos_evento (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    distribucion_id BIGINT NOT NULL REFERENCES distribuciones_evento(id) ON DELETE RESTRICT,
    mesa_id BIGINT,
    tipo_entrada_id INT NOT NULL REFERENCES tipos_entrada(id) ON DELETE RESTRICT,
    etiqueta VARCHAR(50) NOT NULL CHECK (btrim(etiqueta)<>''),
    fila VARCHAR(50),
    x NUMERIC(10,6) NOT NULL CHECK (x BETWEEN 0 AND 1),
    y NUMERIC(10,6) NOT NULL CHECK (y BETWEEN 0 AND 1),
    estado VARCHAR(20) NOT NULL DEFAULT 'DISPONIBLE'
        CHECK (estado IN ('DISPONIBLE','BLOQUEADO','RESERVADO','VENDIDO')),
    CONSTRAINT uq_asientos_distribucion_etiqueta UNIQUE(distribucion_id, etiqueta),
    CONSTRAINT fk_asientos_mesa_tipo FOREIGN KEY(mesa_id, distribucion_id, tipo_entrada_id)
        REFERENCES mesas_evento(id, distribucion_id, tipo_entrada_id) ON DELETE RESTRICT
);
CREATE INDEX idx_asientos_distribucion_estado ON asientos_evento(distribucion_id, estado);
CREATE INDEX idx_asientos_mesa ON asientos_evento(mesa_id);
CREATE INDEX idx_asientos_tipo ON asientos_evento(tipo_entrada_id);

CREATE TABLE asignaciones_asiento (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    asiento_id BIGINT NOT NULL REFERENCES asientos_evento(id) ON DELETE RESTRICT,
    detalle_compra_id INT NOT NULL REFERENCES detalle_compra(id) ON DELETE RESTRICT,
    ordinal_boleto INT NOT NULL CHECK (ordinal_boleto>0),
    estado VARCHAR(20) NOT NULL DEFAULT 'RESERVADA',
    boleto_id BIGINT UNIQUE REFERENCES boletos(id) ON DELETE RESTRICT,
    fecha_reserva TIMESTAMPTZ NOT NULL DEFAULT now(),
    fecha_confirmacion TIMESTAMPTZ,
    fecha_liberacion TIMESTAMPTZ,
    CONSTRAINT uq_asignaciones_detalle_ordinal UNIQUE(detalle_compra_id, ordinal_boleto),
    CONSTRAINT ck_asignaciones_estado CHECK (
        (estado='RESERVADA' AND boleto_id IS NULL AND fecha_confirmacion IS NULL AND fecha_liberacion IS NULL) OR
        (estado='CONFIRMADA' AND boleto_id IS NOT NULL AND fecha_confirmacion IS NOT NULL AND fecha_liberacion IS NULL) OR
        (estado='LIBERADA' AND fecha_liberacion IS NOT NULL AND
            ((boleto_id IS NULL AND fecha_confirmacion IS NULL) OR
             (boleto_id IS NOT NULL AND fecha_confirmacion IS NOT NULL)))),
    CONSTRAINT ck_asignaciones_fechas CHECK (
        (fecha_confirmacion IS NULL OR fecha_confirmacion>=fecha_reserva) AND
        (fecha_liberacion IS NULL OR fecha_liberacion>=COALESCE(fecha_confirmacion,fecha_reserva)))
);
CREATE UNIQUE INDEX uq_asignaciones_asiento_vigente ON asignaciones_asiento(asiento_id)
    WHERE estado IN ('RESERVADA','CONFIRMADA');

CREATE FUNCTION proteger_estructura_numerada() RETURNS trigger
LANGUAGE plpgsql SET search_path=pg_catalog, public AS $$
DECLARE distribucion bigint; publicada boolean;
BEGIN
    IF TG_OP='DELETE' THEN
        RAISE EXCEPTION 'Inventario numerado conserva su historial' USING ERRCODE='23514';
    END IF;
    IF TG_TABLE_NAME='distribuciones_evento' THEN
        -- Serializa configuración con los cambios del evento; no bloquea ventas generales.
        PERFORM 1 FROM public.eventos WHERE id=NEW.evento_id FOR UPDATE;
        IF TG_OP='UPDATE' AND (OLD.evento_id<>NEW.evento_id OR OLD.localidad_id<>NEW.localidad_id OR
            OLD.fecha_creacion<>NEW.fecha_creacion OR OLD.id<>NEW.id OR OLD.estado='PUBLICADA') THEN
            RAISE EXCEPTION 'Distribución publicada o identidad inmutable' USING ERRCODE='23514';
        END IF;
        IF TG_OP='INSERT' AND NEW.estado<>'BORRADOR' THEN
            RAISE EXCEPTION 'Crear distribución en BORRADOR' USING ERRCODE='23514';
        END IF;
        RETURN NEW;
    END IF;
    distribucion:=NEW.distribucion_id;
    SELECT estado='PUBLICADA' INTO publicada FROM public.distribuciones_evento
        WHERE id=distribucion FOR UPDATE;
    IF TG_OP='UPDATE' AND (OLD.id<>NEW.id OR OLD.distribucion_id<>NEW.distribucion_id) THEN
        RAISE EXCEPTION 'Identidad de posición inmutable' USING ERRCODE='23514';
    END IF;
    IF TG_TABLE_NAME='mesas_evento' THEN
        IF publicada THEN
            RAISE EXCEPTION 'Mesa publicada inmutable' USING ERRCODE='23514';
        END IF;
        RETURN NEW;
    END IF;
    IF publicada THEN
        IF TG_OP='INSERT' THEN
            RAISE EXCEPTION 'Distribución publicada no admite cambios estructurales' USING ERRCODE='23514';
        END IF;
        IF (to_jsonb(OLD)-'estado') IS DISTINCT FROM (to_jsonb(NEW)-'estado') THEN
            RAISE EXCEPTION 'Solo cambia el estado operativo de la silla publicada' USING ERRCODE='23514';
        END IF;
    ELSIF TG_TABLE_NAME='asientos_evento' AND NEW.estado NOT IN ('DISPONIBLE','BLOQUEADO') THEN
        RAISE EXCEPTION 'No reservar posiciones de un borrador' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE FUNCTION proteger_asignacion_numerada() RETURNS trigger
LANGUAGE plpgsql SET search_path=pg_catalog, public AS $$
BEGIN
    IF TG_OP='DELETE' THEN
        RAISE EXCEPTION 'No se elimina historial de asignaciones' USING ERRCODE='23514';
    END IF;
    PERFORM 1 FROM public.asientos_evento WHERE id=NEW.asiento_id FOR UPDATE;
    IF TG_OP='INSERT' AND NEW.estado<>'RESERVADA' THEN
        RAISE EXCEPTION 'Toda asignación empieza reservada' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' AND (NEW.id<>OLD.id OR NEW.asiento_id<>OLD.asiento_id OR
        NEW.detalle_compra_id<>OLD.detalle_compra_id OR NEW.ordinal_boleto<>OLD.ordinal_boleto OR
        NEW.fecha_reserva<>OLD.fecha_reserva OR OLD.estado='LIBERADA' OR
        (OLD.estado='CONFIRMADA' AND (NEW.estado<>'LIBERADA' OR NEW.boleto_id IS DISTINCT FROM OLD.boleto_id OR
            NEW.fecha_confirmacion IS DISTINCT FROM OLD.fecha_confirmacion))) THEN
        RAISE EXCEPTION 'Asignación histórica inmutable o transición inválida' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;

-- Evaluación diferida: estado final de toda la transacción, incluidas modificaciones
-- de las tablas operativas previas. No permite habilitar ventas mediante código antiguo.
CREATE FUNCTION validar_inventario_numerado() RETURNS trigger
LANGUAGE plpgsql SET search_path=pg_catalog, public AS $$
DECLARE objetivos bigint[]; registro_actual jsonb; registro_anterior jsonb;
BEGIN
    registro_actual:=to_jsonb(NEW); registro_anterior:=to_jsonb(OLD);
    SELECT array_agg(DISTINCT d.id) INTO objetivos FROM public.distribuciones_evento d WHERE
        (TG_TABLE_NAME='distribuciones_evento' AND d.id=(registro_actual->>'id')::bigint) OR
        (TG_TABLE_NAME IN ('mesas_evento','asientos_evento') AND d.id=(registro_actual->>'distribucion_id')::bigint) OR
        (TG_TABLE_NAME='asignaciones_asiento' AND EXISTS (SELECT 1 FROM public.asientos_evento a WHERE a.id=(registro_actual->>'asiento_id')::bigint AND a.distribucion_id=d.id)) OR
        (TG_TABLE_NAME='eventos' AND d.evento_id=(registro_actual->>'id')::int) OR
        (TG_TABLE_NAME='localidades' AND d.localidad_id=(registro_actual->>'id')::int) OR
        (TG_TABLE_NAME='compras' AND d.evento_id=(registro_actual->>'evento_id')::int) OR
        (TG_TABLE_NAME='tipos_entrada' AND (EXISTS (SELECT 1 FROM public.asientos_evento a WHERE a.distribucion_id=d.id AND a.tipo_entrada_id=(registro_actual->>'id')::int)
            OR (d.evento_id=(registro_actual->>'evento_id')::int AND d.localidad_id=(registro_actual->>'localidad_id')::int)
            OR (d.evento_id=(registro_anterior->>'evento_id')::int AND d.localidad_id=(registro_anterior->>'localidad_id')::int))) OR
        (TG_TABLE_NAME='detalle_compra' AND EXISTS (SELECT 1 FROM public.tipos_entrada t WHERE t.id=COALESCE((registro_actual->>'tipo_entrada_id')::int,(registro_anterior->>'tipo_entrada_id')::int) AND t.evento_id=d.evento_id AND t.localidad_id=d.localidad_id)) OR
        (TG_TABLE_NAME='boletos' AND EXISTS (SELECT 1 FROM public.detalle_compra dc JOIN public.tipos_entrada t ON t.id=dc.tipo_entrada_id
            WHERE dc.id=COALESCE((registro_actual->>'detalle_compra_id')::int,(registro_anterior->>'detalle_compra_id')::int) AND t.evento_id=d.evento_id AND t.localidad_id=d.localidad_id));
    IF objetivos IS NULL THEN RETURN NULL; END IF;
    IF EXISTS (SELECT 1 FROM (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d
        JOIN public.eventos e ON e.id=d.evento_id JOIN public.localidades l ON l.id=d.localidad_id
        WHERE e.recinto_id<>l.recinto_id) OR EXISTS (
        SELECT 1 FROM public.asientos_evento a JOIN (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d ON d.id=a.distribucion_id
        JOIN public.tipos_entrada t ON t.id=a.tipo_entrada_id
        WHERE t.evento_id<>d.evento_id OR t.localidad_id IS DISTINCT FROM d.localidad_id) OR EXISTS (
        SELECT 1 FROM public.mesas_evento m JOIN (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d ON d.id=m.distribucion_id
        JOIN public.tipos_entrada t ON t.id=m.tipo_entrada_id
        WHERE t.evento_id<>d.evento_id OR t.localidad_id IS DISTINCT FROM d.localidad_id) THEN
        RAISE EXCEPTION 'Inventario no corresponde a evento/localidad/tarifa' USING ERRCODE='23514';
    END IF;
    IF EXISTS (SELECT 1 FROM (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d JOIN public.localidades l ON l.id=d.localidad_id
        WHERE (SELECT count(*) FROM public.asientos_evento a WHERE a.distribucion_id=d.id)>LEAST(l.capacidad,2000)
           OR (SELECT count(*) FROM public.mesas_evento m WHERE m.distribucion_id=d.id)>500) THEN
        RAISE EXCEPTION 'Distribución supera capacidad o límites técnicos' USING ERRCODE='23514';
    END IF;
    IF EXISTS (SELECT 1 FROM (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d JOIN public.tipos_entrada t
        ON t.evento_id=d.evento_id AND t.localidad_id=d.localidad_id
        WHERE d.estado='PUBLICADA' AND (NOT EXISTS (SELECT 1 FROM public.asientos_evento a WHERE a.tipo_entrada_id=t.id)
        OR t.cupo_total<>(SELECT count(*) FROM public.asientos_evento a WHERE a.tipo_entrada_id=t.id)
        OR t.cupo_disponible<>(SELECT count(*) FROM public.asientos_evento a WHERE a.tipo_entrada_id=t.id AND a.estado='DISPONIBLE'))) THEN
        RAISE EXCEPTION 'Cupo numerado no conciliado con posiciones' USING ERRCODE='23514';
    END IF;
    IF EXISTS (SELECT 1 FROM (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d WHERE d.estado='PUBLICADA'
        AND NOT EXISTS (SELECT 1 FROM public.asientos_evento a WHERE a.distribucion_id=d.id)) OR EXISTS (
        SELECT 1 FROM public.mesas_evento m JOIN (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d ON d.id=m.distribucion_id
        JOIN public.eventos e ON e.id=d.evento_id WHERE d.estado='PUBLICADA' AND (
            NOT EXISTS (SELECT 1 FROM public.asientos_evento a WHERE a.mesa_id=m.id) OR
            (m.modalidad='COMPLETA' AND (SELECT count(*) FROM public.asientos_evento a WHERE a.mesa_id=m.id)>e.max_boletos_por_compra))) THEN
        RAISE EXCEPTION 'Mesa o distribución sin sillas o fuera del límite de compra' USING ERRCODE='23514';
    END IF;
    IF EXISTS (SELECT 1 FROM public.asignaciones_asiento s
        JOIN public.asientos_evento a ON a.id=s.asiento_id
        JOIN (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d ON d.id=a.distribucion_id
        JOIN public.detalle_compra dc ON dc.id=s.detalle_compra_id
        JOIN public.compras c ON c.id=dc.compra_id LEFT JOIN public.boletos b ON b.id=s.boleto_id
        WHERE d.estado<>'PUBLICADA' OR dc.tipo_entrada_id<>a.tipo_entrada_id OR c.evento_id<>d.evento_id
        OR s.ordinal_boleto>dc.cantidad OR (b.id IS NOT NULL AND b.detalle_compra_id<>dc.id)
        OR (s.estado='RESERVADA' AND c.estado NOT IN ('PENDIENTE','PROCESANDO'))
        OR (s.estado='CONFIRMADA' AND (c.estado<>'COMPLETADA' OR b.estado NOT IN ('VALIDO','USADO')))
        OR (s.estado='LIBERADA' AND ((s.boleto_id IS NOT NULL AND b.estado<>'ANULADO') OR
            (s.boleto_id IS NULL AND c.estado NOT IN ('CANCELADA','EXPIRADA','FALLIDA'))))) THEN
        RAISE EXCEPTION 'Asignación, compra y boleto inconsistentes' USING ERRCODE='23514';
    END IF;
    IF EXISTS (SELECT 1 FROM public.asientos_evento a WHERE a.distribucion_id=ANY(objetivos) AND (
        (a.estado='RESERVADO') IS DISTINCT FROM EXISTS (SELECT 1 FROM public.asignaciones_asiento s WHERE s.asiento_id=a.id AND s.estado='RESERVADA') OR
        (a.estado='VENDIDO') IS DISTINCT FROM EXISTS (SELECT 1 FROM public.asignaciones_asiento s WHERE s.asiento_id=a.id AND s.estado='CONFIRMADA'))) THEN
        RAISE EXCEPTION 'Estado de silla no corresponde a asignación vigente' USING ERRCODE='23514';
    END IF;
    IF EXISTS (SELECT 1 FROM public.detalle_compra dc JOIN public.tipos_entrada t ON t.id=dc.tipo_entrada_id
        JOIN (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d ON d.evento_id=t.evento_id AND d.localidad_id=t.localidad_id
        WHERE d.estado='PUBLICADA' AND dc.cantidad<>(SELECT count(*) FROM public.asignaciones_asiento s WHERE s.detalle_compra_id=dc.id)) THEN
        RAISE EXCEPTION 'Detalle numerado requiere una asignación por boleto' USING ERRCODE='23514';
    END IF;
    IF EXISTS (SELECT 1 FROM public.mesas_evento m JOIN (SELECT * FROM public.distribuciones_evento WHERE id=ANY(objetivos)) d ON d.id=m.distribucion_id
        WHERE d.estado='PUBLICADA' AND m.modalidad='COMPLETA' AND (
            (SELECT count(DISTINCT a.estado) FROM public.asientos_evento a WHERE a.mesa_id=m.id AND a.estado<>'VENDIDO')>1 OR
            EXISTS (SELECT 1 FROM public.asignaciones_asiento s JOIN public.asientos_evento a ON a.id=s.asiento_id
                WHERE a.mesa_id=m.id AND s.estado='RESERVADA' AND
                (SELECT count(*) FROM public.asignaciones_asiento s2 JOIN public.asientos_evento a2 ON a2.id=s2.asiento_id
                 WHERE a2.mesa_id=m.id AND s2.detalle_compra_id=s.detalle_compra_id AND s2.estado='RESERVADA')<>
                (SELECT count(*) FROM public.asientos_evento a3 WHERE a3.mesa_id=m.id)))) THEN
        RAISE EXCEPTION 'Mesa completa requiere reserva o bloqueo íntegro' USING ERRCODE='23514';
    END IF;
    RETURN NULL;
END;
$$;

DO $$
DECLARE tabla text;
BEGIN
    FOREACH tabla IN ARRAY ARRAY['distribuciones_evento','mesas_evento','asientos_evento'] LOOP
        EXECUTE format('CREATE TRIGGER proteger_estructura BEFORE INSERT OR UPDATE OR DELETE ON public.%I FOR EACH ROW EXECUTE FUNCTION public.proteger_estructura_numerada()',tabla);
    END LOOP;
    FOREACH tabla IN ARRAY ARRAY['distribuciones_evento','mesas_evento','asientos_evento','asignaciones_asiento',
        'tipos_entrada','eventos','localidades','compras','detalle_compra','boletos'] LOOP
        EXECUTE format('CREATE CONSTRAINT TRIGGER validar_inventario_numerado AFTER INSERT OR UPDATE OR DELETE ON public.%I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.validar_inventario_numerado()',tabla);
    END LOOP;
END;
$$;
CREATE TRIGGER proteger_asignacion BEFORE INSERT OR UPDATE OR DELETE ON asignaciones_asiento
    FOR EACH ROW EXECUTE FUNCTION proteger_asignacion_numerada();

DO $$
DECLARE tabla text; funcion text;
BEGIN
    FOREACH tabla IN ARRAY ARRAY['distribuciones_evento','mesas_evento','asientos_evento','asignaciones_asiento'] LOOP
        EXECUTE format('ALTER TABLE public.%I OWNER TO flyway',tabla);
        EXECUTE format('REVOKE ALL ON public.%I FROM PUBLIC, promotora_app',tabla);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE ON public.%I TO promotora_app',tabla);
    END LOOP;
    FOREACH funcion IN ARRAY ARRAY['proteger_estructura_numerada','proteger_asignacion_numerada','validar_inventario_numerado'] LOOP
        EXECUTE format('ALTER FUNCTION public.%I() OWNER TO flyway',funcion);
        EXECUTE format('REVOKE ALL ON FUNCTION public.%I() FROM PUBLIC',funcion);
        EXECUTE format('GRANT EXECUTE ON FUNCTION public.%I() TO promotora_app',funcion);
    END LOOP;
END;
$$;
GRANT USAGE, SELECT ON SEQUENCE distribuciones_evento_id_seq, mesas_evento_id_seq,
    asientos_evento_id_seq, asignaciones_asiento_id_seq TO promotora_app;
