-- F1-06: reseñas por evento y cliente.
-- Referencias: docs/BASE_DE_DATOS.md §§3, 4.6 y 12;
-- docs/referencia/schema_v1.sql (solo referencia histórica, no ejecutar).
-- Se conservan tipos, nulabilidad, defaults, unicidad y FKs de v1 para
-- esta tabla existente; id pasa a identity ALWAYS y fecha a TIMESTAMPTZ.
-- Las reseñas nacen PENDIENTE y su calificación está entre 1 y 5.
-- La elegibilidad con boleto USADO, propiedad, moderación y retiro mediante
-- cambio de estado corresponden a los casos de uso de F8, no a esta migración.

CREATE TABLE resenas (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    evento_id INT NOT NULL,
    cliente_id INT NOT NULL,
    calificacion SMALLINT NOT NULL,
    comentario TEXT,
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    fecha_creacion TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT fk_resenas_evento FOREIGN KEY (evento_id)
        REFERENCES eventos (id) ON DELETE CASCADE,
    CONSTRAINT fk_resenas_cliente FOREIGN KEY (cliente_id)
        REFERENCES clientes (id) ON DELETE CASCADE,
    CONSTRAINT uq_resenas_evento_cliente UNIQUE (evento_id, cliente_id),
    CONSTRAINT ck_resenas_calificacion CHECK (calificacion BETWEEN 1 AND 5),
    CONSTRAINT ck_resenas_estado CHECK (estado IN ('PENDIENTE', 'APROBADA', 'RECHAZADA'))
);
