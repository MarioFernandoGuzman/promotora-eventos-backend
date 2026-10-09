DO $$ BEGIN
    IF (SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'flyway_schema_history')<>26
       OR (SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name<>'flyway_schema_history')<>198
       OR (SELECT count(*) FROM pg_views WHERE schemaname='bi')<>16 THEN
        RAISE EXCEPTION 'Conteo de tablas, columnas o vistas incorrecto';
    END IF;
    IF NOT EXISTS(SELECT 1 FROM pg_extension WHERE extname='vector')
       OR NOT EXISTS(SELECT 1 FROM pg_indexes WHERE indexname='idx_chunks_rag_embedding_hnsw' AND indexdef LIKE '%USING hnsw%vector_cosine_ops%') THEN
        RAISE EXCEPTION 'Falta pgvector/HNSW coseno';
    END IF;
    IF (SELECT count(*) FROM pg_trigger WHERE tgdeferrable AND tginitdeferred AND NOT tgisinternal)<>4 THEN
        RAISE EXCEPTION 'Faltan los cuatro triggers diferidos de perfil';
    END IF;
    IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname IN ('flyway','promotora_app','bi_reader')
              AND (rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN
        RAISE EXCEPTION 'Rol de grupo con privilegios indebidos';
    END IF;
    IF EXISTS(SELECT 1 FROM usuarios) OR EXISTS(SELECT 1 FROM movimientos_financieros)
       OR (SELECT count(*) FROM categorias_evento)<>6
       OR (SELECT count(*) FROM cuentas_financieras WHERE moneda='GTQ' AND saldo=0 AND estado='ACTIVA' AND tipo IN ('PLATAFORMA','IMPUESTOS'))<>2 THEN
        RAISE EXCEPTION 'Las migraciones siembran datos fuera del alcance de produccion';
    END IF;
END; $$;
