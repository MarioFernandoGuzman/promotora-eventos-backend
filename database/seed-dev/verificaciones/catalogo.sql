SELECT jsonb_agg(jsonb_build_object(
    'tabla', t.relname,
    'columnas', (SELECT jsonb_agg(jsonb_build_object(
        'nombre', a.attname, 'tipo', format_type(a.atttypid,a.atttypmod),
        'nullable', NOT a.attnotnull,
        'default', CASE WHEN a.attidentity='a' THEN 'GENERATED ALWAYS AS IDENTITY' ELSE pg_get_expr(d.adbin,d.adrelid) END,
        'claves', (SELECT string_agg(CASE c.contype WHEN 'p' THEN 'PK' WHEN 'f' THEN 'FK' WHEN 'u' THEN 'UNIQUE' END || ' ('||c.conname||')', '; ' ORDER BY c.conname)
            FROM pg_constraint c WHERE c.conrelid=t.oid AND c.contype IN ('p','f','u') AND a.attnum=ANY(c.conkey))
    ) ORDER BY a.attnum) FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
        WHERE a.attrelid=t.oid AND a.attnum>0 AND NOT a.attisdropped),
    'restricciones', (SELECT jsonb_agg(jsonb_build_object('nombre',c.conname,'definicion',pg_get_constraintdef(c.oid,true)) ORDER BY c.contype,c.conname)
        FROM pg_constraint c WHERE c.conrelid=t.oid AND c.contype IN ('p','f','u','c')),
    'indices', (SELECT jsonb_agg(pg_get_indexdef(i.indexrelid) ORDER BY i.indexrelid::regclass::text)
        FROM pg_index i WHERE i.indrelid=t.oid AND NOT EXISTS(SELECT 1 FROM pg_constraint c WHERE c.conindid=i.indexrelid))
) ORDER BY t.relname)
FROM pg_class t JOIN pg_namespace n ON n.oid=t.relnamespace
WHERE n.nspname='public' AND t.relkind='r' AND t.relname<>'flyway_schema_history';
