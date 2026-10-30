-- EXT-NUM-07: revisión de integridad del ciclo completo.
-- V12 ya está commiteada: corregir solo mediante migración adicional.
-- Sin nuevas tablas/columnas; revisa también cambios de FK de detalle/boleto
-- y exige un boleto por asignación en compras numeradas completadas.

CREATE OR REPLACE FUNCTION validar_inventario_numerado() RETURNS trigger
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
    -- También se revisa la relación histórica cuando cambia el FK del padre.
    SELECT array_agg(DISTINCT id) INTO objetivos FROM (
        SELECT unnest(objetivos) AS id
        UNION ALL
        SELECT a.distribucion_id FROM public.asignaciones_asiento s JOIN public.asientos_evento a ON a.id=s.asiento_id
        WHERE (TG_TABLE_NAME='detalle_compra' AND s.detalle_compra_id=COALESCE((registro_actual->>'id')::int,(registro_anterior->>'id')::int))
           OR (TG_TABLE_NAME='boletos' AND s.boleto_id=COALESCE((registro_actual->>'id')::bigint,(registro_anterior->>'id')::bigint))
    ) afectadas;
    IF objetivos IS NULL THEN RETURN NULL; END IF;
    PERFORM id FROM public.distribuciones_evento WHERE id=ANY(objetivos) ORDER BY id FOR UPDATE;
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
    IF EXISTS (SELECT 1 FROM public.detalle_compra dc JOIN public.tipos_entrada t ON t.id=dc.tipo_entrada_id
        JOIN public.compras c ON c.id=dc.compra_id
        JOIN public.distribuciones_evento d ON d.evento_id=t.evento_id AND d.localidad_id=t.localidad_id
        WHERE d.id=ANY(objetivos) AND d.estado='PUBLICADA' AND (
            (c.estado='COMPLETADA' AND (dc.cantidad<>(SELECT count(*) FROM public.boletos b WHERE b.detalle_compra_id=dc.id)
             OR EXISTS (SELECT 1 FROM public.boletos b WHERE b.detalle_compra_id=dc.id AND NOT EXISTS (
                 SELECT 1 FROM public.asignaciones_asiento s WHERE s.boleto_id=b.id AND s.detalle_compra_id=dc.id))))
            OR (c.estado<>'COMPLETADA' AND EXISTS (SELECT 1 FROM public.boletos b WHERE b.detalle_compra_id=dc.id))
        )) THEN
        RAISE EXCEPTION 'Cada boleto numerado requiere su asignación histórica' USING ERRCODE='23514';
    END IF;
    RETURN NULL;
END;
$$;
