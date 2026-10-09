-- =====================================================================
-- Proyecto: Promotora de Eventos (conciertos, partidos, vigilias, cultos,
--           carreras, meet & greets)
-- Motor: PostgreSQL 14+
-- =====================================================================
-- REFERENCIA HISTÓRICA (v1). NO es una migración y NO debe ejecutarse
-- con Flyway ni contra una BD con datos (contiene DROP TABLE).
-- El esquema vigente se construye con database/migrations/ (v2).
-- =====================================================================

-- ---------------------------------------------------------------------
-- Limpieza (idempotente para poder re-ejecutar el script en desarrollo)
-- ---------------------------------------------------------------------
DROP TABLE IF EXISTS notificaciones CASCADE;
DROP TABLE IF EXISTS documentos_rag CASCADE;
DROP TABLE IF EXISTS logs_agente_ia CASCADE;
DROP TABLE IF EXISTS logs_auditoria CASCADE;
DROP TABLE IF EXISTS resenas CASCADE;
DROP TABLE IF EXISTS boletos CASCADE;
DROP TABLE IF EXISTS detalle_compra CASCADE;
DROP TABLE IF EXISTS compras CASCADE;
DROP TABLE IF EXISTS tipos_entrada CASCADE;
DROP TABLE IF EXISTS eventos CASCADE;
DROP TABLE IF EXISTS presentadores CASCADE;
DROP TABLE IF EXISTS categorias_evento CASCADE;
DROP TABLE IF EXISTS localidades CASCADE;
DROP TABLE IF EXISTS recintos CASCADE;
DROP TABLE IF EXISTS duenos_recinto CASCADE;
DROP TABLE IF EXISTS administradores CASCADE;
DROP TABLE IF EXISTS clientes CASCADE;
DROP TABLE IF EXISTS usuarios CASCADE;

-- =====================================================================
-- 1. USUARIOS Y ROLES
-- =====================================================================

CREATE TABLE usuarios (
    id              SERIAL PRIMARY KEY,
    email           VARCHAR(150) NOT NULL UNIQUE,
    password_hash   VARCHAR(255) NOT NULL,
    nombre          VARCHAR(100) NOT NULL,
    apellido        VARCHAR(100) NOT NULL,
    rol             VARCHAR(20)  NOT NULL
                    CHECK (rol IN ('CLIENTE', 'ADMINISTRADOR', 'DUENO_RECINTO')),
    estado          VARCHAR(20)  NOT NULL DEFAULT 'ACTIVO'
                    CHECK (estado IN ('ACTIVO', 'INACTIVO', 'SUSPENDIDO')),
    fecha_registro  TIMESTAMP    NOT NULL DEFAULT now()
);

CREATE TABLE clientes (
    id                 SERIAL PRIMARY KEY,
    usuario_id         INT NOT NULL UNIQUE REFERENCES usuarios(id) ON DELETE CASCADE,
    fecha_nacimiento   DATE,
    telefono           VARCHAR(20)
);

CREATE TABLE administradores (
    id             SERIAL PRIMARY KEY,
    usuario_id     INT NOT NULL UNIQUE REFERENCES usuarios(id) ON DELETE CASCADE,
    nivel_acceso   VARCHAR(20) NOT NULL DEFAULT 'ESTANDAR'
                   CHECK (nivel_acceso IN ('ESTANDAR', 'SUPERADMIN'))
);

CREATE TABLE duenos_recinto (
    id             SERIAL PRIMARY KEY,
    usuario_id     INT NOT NULL UNIQUE REFERENCES usuarios(id) ON DELETE CASCADE,
    empresa        VARCHAR(150) NOT NULL,
    nit            VARCHAR(20)
);

-- =====================================================================
-- 2. RECINTOS Y LOCALIDADES
-- =====================================================================

CREATE TABLE recintos (
    id                SERIAL PRIMARY KEY,
    dueno_id          INT NOT NULL REFERENCES duenos_recinto(id) ON DELETE RESTRICT,
    nombre            VARCHAR(150) NOT NULL,
    direccion         VARCHAR(255),
    ciudad            VARCHAR(100),
    latitud           NUMERIC(9,6),
    longitud          NUMERIC(9,6),
    imagen_plano_url  VARCHAR(255),
    capacidad_total   INT CHECK (capacidad_total > 0),
    fecha_creacion    TIMESTAMP NOT NULL DEFAULT now()
);

CREATE TABLE localidades (
    id           SERIAL PRIMARY KEY,
    recinto_id   INT NOT NULL REFERENCES recintos(id) ON DELETE CASCADE,
    nombre       VARCHAR(100) NOT NULL,
    capacidad    INT NOT NULL CHECK (capacidad > 0),
    UNIQUE (recinto_id, nombre)
);

-- =====================================================================
-- 3. CATÁLOGOS DE EVENTOS
-- =====================================================================

CREATE TABLE categorias_evento (
    id            SERIAL PRIMARY KEY,
    nombre        VARCHAR(50) NOT NULL UNIQUE,
    descripcion   VARCHAR(255)
);

CREATE TABLE presentadores (
    id           SERIAL PRIMARY KEY,
    nombre       VARCHAR(150) NOT NULL,
    tipo         VARCHAR(20) NOT NULL
                 CHECK (tipo IN ('ARTISTA', 'EQUIPO', 'PRESENTADOR', 'ORGANIZACION')),
    biografia    TEXT,
    imagen_url   VARCHAR(255)
);

-- =====================================================================
-- 4. EVENTOS Y VENTA DE BOLETOS
-- =====================================================================

CREATE TABLE eventos (
    id              SERIAL PRIMARY KEY,
    recinto_id      INT NOT NULL REFERENCES recintos(id) ON DELETE RESTRICT,
    categoria_id    INT NOT NULL REFERENCES categorias_evento(id) ON DELETE RESTRICT,
    presentador_id  INT REFERENCES presentadores(id) ON DELETE SET NULL,
    nombre          VARCHAR(150) NOT NULL,
    descripcion     TEXT,
    fecha_inicio    TIMESTAMP NOT NULL,
    fecha_fin       TIMESTAMP,
    estado          VARCHAR(20) NOT NULL DEFAULT 'PROGRAMADO'
                    CHECK (estado IN ('PROGRAMADO', 'EN_VENTA', 'AGOTADO', 'FINALIZADO', 'CANCELADO')),
    imagen_url      VARCHAR(255),
    fecha_creacion  TIMESTAMP NOT NULL DEFAULT now(),
    CHECK (fecha_fin IS NULL OR fecha_fin >= fecha_inicio)
);

-- Tipo de entrada: cubre entrada general, venta por localidad física
-- y categorías de carrera (5k, 10k, 21k, 42k) con la misma tabla.
CREATE TABLE tipos_entrada (
    id                SERIAL PRIMARY KEY,
    evento_id         INT NOT NULL REFERENCES eventos(id) ON DELETE CASCADE,
    localidad_id      INT REFERENCES localidades(id) ON DELETE SET NULL,
    nombre            VARCHAR(100) NOT NULL,
    precio            NUMERIC(10,2) NOT NULL CHECK (precio >= 0),
    cupo_total        INT NOT NULL CHECK (cupo_total >= 0),
    cupo_disponible   INT NOT NULL CHECK (cupo_disponible >= 0),
    CHECK (cupo_disponible <= cupo_total)
);

CREATE TABLE compras (
    id                SERIAL PRIMARY KEY,
    cliente_id        INT NOT NULL REFERENCES clientes(id) ON DELETE RESTRICT,
    fecha_compra      TIMESTAMP NOT NULL DEFAULT now(),
    estado            VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE'
                      CHECK (estado IN ('PENDIENTE', 'PROCESANDO', 'COMPLETADA', 'CANCELADA', 'FALLIDA')),
    total             NUMERIC(10,2) NOT NULL DEFAULT 0 CHECK (total >= 0),
    metodo_pago       VARCHAR(30),
    referencia_pago   VARCHAR(100)
);

CREATE TABLE detalle_compra (
    id                SERIAL PRIMARY KEY,
    compra_id         INT NOT NULL REFERENCES compras(id) ON DELETE CASCADE,
    tipo_entrada_id   INT NOT NULL REFERENCES tipos_entrada(id) ON DELETE RESTRICT,
    cantidad          INT NOT NULL CHECK (cantidad > 0),
    precio_unitario   NUMERIC(10,2) NOT NULL CHECK (precio_unitario >= 0),
    subtotal          NUMERIC(10,2) NOT NULL CHECK (subtotal >= 0)
);

CREATE TABLE boletos (
    id                  SERIAL PRIMARY KEY,
    detalle_compra_id   INT NOT NULL REFERENCES detalle_compra(id) ON DELETE CASCADE,
    codigo_qr           VARCHAR(100) NOT NULL UNIQUE,
    estado              VARCHAR(20) NOT NULL DEFAULT 'VALIDO'
                        CHECK (estado IN ('VALIDO', 'USADO', 'ANULADO')),
    fecha_emision       TIMESTAMP NOT NULL DEFAULT now()
);

-- =====================================================================
-- 5. RESEÑAS
-- =====================================================================

CREATE TABLE resenas (
    id               SERIAL PRIMARY KEY,
    evento_id        INT NOT NULL REFERENCES eventos(id) ON DELETE CASCADE,
    cliente_id       INT NOT NULL REFERENCES clientes(id) ON DELETE CASCADE,
    calificacion     SMALLINT NOT NULL CHECK (calificacion BETWEEN 1 AND 5),
    comentario       TEXT,
    estado           VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE'
                     CHECK (estado IN ('PENDIENTE', 'APROBADA', 'RECHAZADA')),
    fecha_creacion   TIMESTAMP NOT NULL DEFAULT now(),
    UNIQUE (evento_id, cliente_id)
);

-- =====================================================================
-- 6. TRAZABILIDAD, AGENTE DE IA Y MENSAJERÍA ASÍNCRONA
-- =====================================================================

CREATE TABLE logs_auditoria (
    id                 BIGSERIAL PRIMARY KEY,
    usuario_id         INT REFERENCES usuarios(id) ON DELETE SET NULL,
    entidad_afectada   VARCHAR(50) NOT NULL,
    accion             VARCHAR(20) NOT NULL
                       CHECK (accion IN ('CREATE', 'UPDATE', 'DELETE', 'LOGIN', 'LOGOUT')),
    detalle            JSONB,
    ip_origen          VARCHAR(45),
    fecha_hora         TIMESTAMP NOT NULL DEFAULT now()
);

CREATE TABLE logs_agente_ia (
    id                    BIGSERIAL PRIMARY KEY,
    usuario_id            INT REFERENCES usuarios(id) ON DELETE SET NULL,
    consulta              TEXT NOT NULL,
    respuesta             TEXT,
    fuentes_citadas       JSONB,
    tiempo_respuesta_ms   INT,
    fecha_hora            TIMESTAMP NOT NULL DEFAULT now()
);

CREATE TABLE documentos_rag (
    id               SERIAL PRIMARY KEY,
    titulo           VARCHAR(200) NOT NULL,
    tipo_documento   VARCHAR(50),
    contenido        TEXT,
    ruta_archivo     VARCHAR(255),
    version          INT NOT NULL DEFAULT 1,
    fecha_carga      TIMESTAMP NOT NULL DEFAULT now()
);

-- Bitácora de mensajes despachados vía RabbitMQ/Kafka (confirmaciones,
-- recordatorios, etc.). Sirve como evidencia de que el flujo asíncrono corrió.
CREATE TABLE notificaciones (
    id                BIGSERIAL PRIMARY KEY,
    usuario_id        INT REFERENCES usuarios(id) ON DELETE CASCADE,
    tipo              VARCHAR(50) NOT NULL,
    estado            VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE'
                      CHECK (estado IN ('PENDIENTE', 'ENVIADO', 'FALLIDO')),
    mensaje           TEXT,
    fecha_creacion    TIMESTAMP NOT NULL DEFAULT now(),
    fecha_envio       TIMESTAMP
);

-- =====================================================================
-- 7. ÍNDICES DE APOYO (consultas frecuentes / reportería)
-- =====================================================================

CREATE INDEX idx_recintos_dueno            ON recintos(dueno_id);
CREATE INDEX idx_localidades_recinto       ON localidades(recinto_id);
CREATE INDEX idx_eventos_recinto           ON eventos(recinto_id);
CREATE INDEX idx_eventos_presentador       ON eventos(presentador_id);
CREATE INDEX idx_eventos_fecha_inicio      ON eventos(fecha_inicio);
CREATE INDEX idx_tipos_entrada_evento      ON tipos_entrada(evento_id);
CREATE INDEX idx_compras_cliente           ON compras(cliente_id);
CREATE INDEX idx_compras_fecha             ON compras(fecha_compra);
CREATE INDEX idx_detalle_compra_compra     ON detalle_compra(compra_id);
CREATE INDEX idx_boletos_detalle           ON boletos(detalle_compra_id);
CREATE INDEX idx_resenas_evento            ON resenas(evento_id);
CREATE INDEX idx_logs_auditoria_fecha      ON logs_auditoria(fecha_hora);
CREATE INDEX idx_logs_agente_fecha         ON logs_agente_ia(fecha_hora);
CREATE INDEX idx_notificaciones_estado     ON notificaciones(estado);

-- =====================================================================
-- 8. DATOS SEMILLA (catálogos base)
-- =====================================================================

INSERT INTO categorias_evento (nombre, descripcion) VALUES
    ('Concierto',    'Presentaciones musicales de artistas o bandas'),
    ('Partido',      'Encuentros deportivos'),
    ('Vigilia',      'Eventos conmemorativos o de oración nocturna'),
    ('Culto',        'Servicios religiosos'),
    ('Carrera',      'Carreras pedestres (5k, 10k, 21k, 42k)'),
    ('Meet & Greet', 'Encuentros con artistas o figuras públicas');
