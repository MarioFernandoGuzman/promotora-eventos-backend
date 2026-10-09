-- F1-02: recintos y localidades con datos para el mapa.
-- Referencias: docs/BASE_DE_DATOS.md §§3, 4.2 y 12;
-- docs/referencia/schema_v1.sql (solo referencia histórica, no ejecutar).
-- Se conservan tipos, claves, nulabilidad y defaults de v1, salvo ajustes v2:
-- ids GENERATED ALWAYS AS IDENTITY, fechas/hora TIMESTAMPTZ y baja lógica.
-- direccion VARCHAR(255), ciudad VARCHAR(100) y capacidad_total nullable
-- se heredan de v1; el CHECK exige capacidad_total > 0 cuando tiene valor.
-- Suposiciones aprobadas: geometria JSONB y color VARCHAR(7) son nullable
-- y no tienen default, para permitir localidades sin mapa configurado.
-- activo es BOOLEAN NOT NULL DEFAULT true en ambas tablas.
-- Se mantiene ON DELETE CASCADE de localidades a recintos según el modelo;
-- la baja de catálogos se realiza con activo = false.
-- Índices adicionales y triggers de capacidad se incorporan en V9 (F1-07).

CREATE TABLE recintos (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    dueno_id INT NOT NULL,
    nombre VARCHAR(150) NOT NULL,
    direccion VARCHAR(255),
    ciudad VARCHAR(100),
    latitud NUMERIC(9,6),
    longitud NUMERIC(9,6),
    imagen_plano_url VARCHAR(255),
    capacidad_total INT,
    activo BOOLEAN NOT NULL DEFAULT true,
    fecha_creacion TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT fk_recintos_dueno FOREIGN KEY (dueno_id)
        REFERENCES duenos_recinto (id) ON DELETE RESTRICT,
    CONSTRAINT ck_recintos_capacidad_total CHECK (capacidad_total > 0)
);

CREATE TABLE localidades (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    recinto_id INT NOT NULL,
    nombre VARCHAR(100) NOT NULL,
    capacidad INT NOT NULL,
    geometria JSONB,
    color VARCHAR(7),
    activo BOOLEAN NOT NULL DEFAULT true,
    CONSTRAINT fk_localidades_recinto FOREIGN KEY (recinto_id)
        REFERENCES recintos (id) ON DELETE CASCADE,
    CONSTRAINT uq_localidades_recinto_nombre UNIQUE (recinto_id, nombre),
    CONSTRAINT ck_localidades_capacidad CHECK (capacidad > 0)
);
