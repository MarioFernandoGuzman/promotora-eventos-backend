-- F1-06: notificaciones, outbox e idempotencia de consumidores.
-- Referencias: docs/BASE_DE_DATOS.md §§3, 4.8 y 12;
-- docs/referencia/schema_v1.sql (solo referencia histórica, no ejecutar).
-- Notificaciones conserva tipos, nulabilidad y defaults de v1 salvo identity
-- ALWAYS, fechas TIMESTAMPTZ y FK a usuarios ON DELETE RESTRICT.
-- Suposiciones: outbox.id BIGINT identity, estable durante publicaciones y
-- reintentos. agregado, agregado_id y tipo son VARCHAR sin límite declarado
-- NOT NULL sin default; payload es JSONB NOT NULL sin default.
-- publicado BOOLEAN NOT NULL DEFAULT false, intentos INT NOT NULL DEFAULT 0,
-- fecha TIMESTAMPTZ NOT NULL DEFAULT now(); fecha_publicado nullable sin default.
-- mensajes_procesados tiene PK compuesta (mensaje_id BIGINT, consumidor
-- VARCHAR(100)), ambos NOT NULL sin default. El mensaje_id transporta el ID
-- estable del outbox; no tiene FK según el diseño de infraestructura.
-- fecha TIMESTAMPTZ NOT NULL DEFAULT now(). Cada consumidor registra su
-- procesamiento en la misma transacción que los efectos del mensaje.
-- Índices adicionales, incluido el parcial del outbox, se crean en V9.
-- El relay, ACKs, reintentos/DLQ y envío de correo se implementan en F6.

CREATE TABLE notificaciones (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id INT,
    tipo VARCHAR(50) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    mensaje TEXT,
    fecha_creacion TIMESTAMPTZ NOT NULL DEFAULT now(),
    fecha_envio TIMESTAMPTZ,
    CONSTRAINT fk_notificaciones_usuario FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_notificaciones_estado CHECK (estado IN ('PENDIENTE', 'ENVIADO', 'FALLIDO'))
);

CREATE TABLE outbox_mensajes (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    agregado VARCHAR NOT NULL,
    agregado_id VARCHAR NOT NULL,
    tipo VARCHAR NOT NULL,
    payload JSONB NOT NULL,
    publicado BOOLEAN NOT NULL DEFAULT false,
    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
    fecha_publicado TIMESTAMPTZ,
    intentos INT NOT NULL DEFAULT 0
);

CREATE TABLE mensajes_procesados (
    mensaje_id BIGINT NOT NULL,
    consumidor VARCHAR(100) NOT NULL,
    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT pk_mensajes_procesados PRIMARY KEY (mensaje_id, consumidor)
);
