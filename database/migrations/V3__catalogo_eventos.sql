-- F1-03: catálogo de eventos, presentadores y tipos de entrada.
-- Referencias: docs/BASE_DE_DATOS.md §§3, 4.3, 5 y 12;
-- docs/referencia/schema_v1.sql (solo referencia histórica, no ejecutar).
-- Se conservan tipos, claves, nulabilidad y defaults de v1, salvo ajustes v2:
-- ids GENERATED ALWAYS AS IDENTITY, fechas/hora TIMESTAMPTZ, estado BORRADOR,
-- baja lógica y relación N:M en lugar de eventos.presentador_id.
-- Suposiciones aprobadas: creado_por INT NOT NULL, FK a usuarios RESTRICT;
-- fecha_inicio_venta y fecha_fin_venta nullable y sin default para borradores;
-- el CHECK compara su orden cuando ambas fechas tienen valor.
-- max_boletos_por_compra es INT NOT NULL DEFAULT 10, con CHECK > 0.
-- evento_presentadores.rol es VARCHAR(30) nullable, sin default ni lista
-- cerrada; sus dos FKs son NOT NULL y forman la PK compuesta documentada.
-- activo es BOOLEAN NOT NULL DEFAULT true en los catálogos con baja lógica.
-- Índices adicionales y triggers de capacidad y coherencia de recinto/
-- localidad/evento se incorporan en V9__indices_triggers_permisos.sql (F1-07).
-- No se siembran categorías ni datos de prueba en esta migración.

CREATE TABLE categorias_evento (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    descripcion VARCHAR(255),
    activo BOOLEAN NOT NULL DEFAULT true,
    CONSTRAINT uq_categorias_evento_nombre UNIQUE (nombre)
);

CREATE TABLE presentadores (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(150) NOT NULL,
    tipo VARCHAR(20) NOT NULL,
    biografia TEXT,
    imagen_url VARCHAR(255),
    activo BOOLEAN NOT NULL DEFAULT true,
    CONSTRAINT ck_presentadores_tipo CHECK (
        tipo IN ('ARTISTA', 'EQUIPO', 'PRESENTADOR', 'ORGANIZACION')
    )
);

CREATE TABLE eventos (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    recinto_id INT NOT NULL,
    categoria_id INT NOT NULL,
    creado_por INT NOT NULL,
    nombre VARCHAR(150) NOT NULL,
    descripcion TEXT,
    fecha_inicio TIMESTAMPTZ NOT NULL,
    fecha_fin TIMESTAMPTZ,
    fecha_inicio_venta TIMESTAMPTZ,
    fecha_fin_venta TIMESTAMPTZ,
    max_boletos_por_compra INT NOT NULL DEFAULT 10,
    estado VARCHAR(20) NOT NULL DEFAULT 'BORRADOR',
    imagen_url VARCHAR(255),
    fecha_creacion TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT fk_eventos_recinto FOREIGN KEY (recinto_id)
        REFERENCES recintos (id) ON DELETE RESTRICT,
    CONSTRAINT fk_eventos_categoria FOREIGN KEY (categoria_id)
        REFERENCES categorias_evento (id) ON DELETE RESTRICT,
    CONSTRAINT fk_eventos_creador FOREIGN KEY (creado_por)
        REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_eventos_fechas CHECK (
        fecha_fin IS NULL OR fecha_fin >= fecha_inicio
    ),
    CONSTRAINT ck_eventos_ventana_venta CHECK (
        fecha_inicio_venta IS NULL OR fecha_fin_venta IS NULL
        OR fecha_fin_venta >= fecha_inicio_venta
    ),
    CONSTRAINT ck_eventos_max_boletos CHECK (max_boletos_por_compra > 0),
    CONSTRAINT ck_eventos_estado CHECK (
        estado IN ('BORRADOR', 'PROGRAMADO', 'EN_VENTA', 'AGOTADO', 'FINALIZADO', 'CANCELADO')
    )
);

CREATE TABLE evento_presentadores (
    evento_id INT NOT NULL,
    presentador_id INT NOT NULL,
    rol VARCHAR(30),
    CONSTRAINT pk_evento_presentadores PRIMARY KEY (evento_id, presentador_id),
    CONSTRAINT fk_evento_presentadores_evento FOREIGN KEY (evento_id)
        REFERENCES eventos (id) ON DELETE CASCADE,
    CONSTRAINT fk_evento_presentadores_presentador FOREIGN KEY (presentador_id)
        REFERENCES presentadores (id) ON DELETE RESTRICT
);

CREATE TABLE tipos_entrada (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    evento_id INT NOT NULL,
    localidad_id INT,
    nombre VARCHAR(100) NOT NULL,
    precio NUMERIC(10,2) NOT NULL,
    cupo_total INT NOT NULL,
    cupo_disponible INT NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT true,
    CONSTRAINT fk_tipos_entrada_evento FOREIGN KEY (evento_id)
        REFERENCES eventos (id) ON DELETE CASCADE,
    CONSTRAINT fk_tipos_entrada_localidad FOREIGN KEY (localidad_id)
        REFERENCES localidades (id) ON DELETE RESTRICT,
    CONSTRAINT ck_tipos_entrada_precio CHECK (precio >= 0),
    CONSTRAINT ck_tipos_entrada_cupo_total CHECK (cupo_total >= 0),
    CONSTRAINT ck_tipos_entrada_cupo_disponible CHECK (
        cupo_disponible >= 0 AND cupo_disponible <= cupo_total
    )
);
