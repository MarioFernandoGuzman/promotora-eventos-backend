-- F1-06: auditoría, documentos/chunks RAG y logs del agente público.
-- Referencias: docs/BASE_DE_DATOS.md §§3, 4.7, 4.8 y 12;
-- docs/referencia/schema_v1.sql (solo referencia histórica, no ejecutar).
-- Campos existentes conservan tipos, nulabilidad y defaults de v1, salvo
-- identity ALWAYS, fechas TIMESTAMPTZ y FKs hacia usuarios ON DELETE RESTRICT.
-- Suposiciones: documentos.estado VARCHAR(20) NOT NULL DEFAULT PENDIENTE y
-- subido_por INT NOT NULL (administrador que ingesta el documento).
-- chunks.id BIGINT identity por volumen; documento_id, orden, contenido y
-- embedding son obligatorios sin default. pagina INT y metadata JSONB son
-- opcionales sin default. FK de chunks a documentos usa CASCADE según v2.
-- logs_agente_ia.session_id UUID y estado VARCHAR(20) son obligatorios sin
-- default: el caso de uso asigna la sesión y el resultado de cada consulta.
-- modelo VARCHAR sin límite, tokens_entrada/tokens_salida INT y entidad_id
-- VARCHAR(50) son opcionales sin default, incluso cuando la consulta falla.
-- vector(1536) e HNSW/coseno corresponden al modelo de embeddings aprobado.
-- Cambiar de modelo obliga a reindexar; aquí no se llama a OpenAI ni se
-- ingesta contenido. Los triggers de inmutabilidad y REVOKE de ambos logs
-- se incorporan en V9 (F1-07); el agente solo consultará información pública.

CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE documentos_rag (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    titulo VARCHAR(200) NOT NULL,
    tipo_documento VARCHAR(50),
    contenido TEXT,
    ruta_archivo VARCHAR(255),
    version INT NOT NULL DEFAULT 1,
    fecha_carga TIMESTAMPTZ NOT NULL DEFAULT now(),
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    subido_por INT NOT NULL,
    CONSTRAINT fk_documentos_rag_usuario FOREIGN KEY (subido_por)
        REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_documentos_rag_estado CHECK (estado IN ('PENDIENTE', 'INDEXADO', 'ERROR', 'INACTIVO'))
);

CREATE TABLE chunks_rag (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    documento_id INT NOT NULL,
    orden INT NOT NULL,
    pagina INT,
    contenido TEXT NOT NULL,
    embedding vector(1536) NOT NULL,
    metadata JSONB,
    CONSTRAINT fk_chunks_rag_documento FOREIGN KEY (documento_id)
        REFERENCES documentos_rag (id) ON DELETE CASCADE
);

CREATE INDEX idx_chunks_rag_embedding_hnsw
    ON chunks_rag USING hnsw (embedding vector_cosine_ops);

CREATE TABLE logs_auditoria (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id INT,
    entidad_afectada VARCHAR(50) NOT NULL,
    entidad_id VARCHAR(50),
    accion VARCHAR(20) NOT NULL,
    detalle JSONB,
    ip_origen VARCHAR(45),
    fecha_hora TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT fk_logs_auditoria_usuario FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_logs_auditoria_accion CHECK (
        accion IN ('CREATE', 'UPDATE', 'DELETE', 'LOGIN', 'LOGOUT', 'LOGIN_FAILED',
                   'PURCHASE', 'PAYMENT', 'REFUND', 'VALIDATE', 'MODERATE', 'EXPORT', 'INGEST')
    )
);

CREATE TABLE logs_agente_ia (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id INT,
    consulta TEXT NOT NULL,
    respuesta TEXT,
    fuentes_citadas JSONB,
    tiempo_respuesta_ms INT,
    fecha_hora TIMESTAMPTZ NOT NULL DEFAULT now(),
    session_id UUID NOT NULL,
    modelo VARCHAR,
    tokens_entrada INT,
    tokens_salida INT,
    estado VARCHAR(20) NOT NULL,
    CONSTRAINT fk_logs_agente_ia_usuario FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_logs_agente_ia_estado CHECK (estado IN ('OK', 'SIN_CONTEXTO', 'ERROR', 'RECHAZADA'))
);
