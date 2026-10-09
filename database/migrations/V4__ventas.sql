-- F1-04: compras, detalle de compra y boletos.
-- Referencias: docs/BASE_DE_DATOS.md §§3, 4.4, 5 y 12;
-- docs/referencia/schema_v1.sql (solo referencia histórica, no ejecutar).
-- Tipos, claves, nulabilidad y defaults heredados de v1, salvo ajustes v2:
-- ids GENERATED ALWAYS AS IDENTITY; boletos.id BIGINT; fechas TIMESTAMPTZ;
-- FKs RESTRICT; metodo_pago/referencia_pago pasan a pagos en V5.
-- Suposiciones aprobadas: public_id UUID NOT NULL UNIQUE con gen_random_uuid();
-- compras.evento_id, expira_en, idempotency_key, moneda y componentes
-- monetarios son NOT NULL. expira_en e idempotency_key no tienen default;
-- moneda usa GTQ y los componentes monetarios usan 0.
-- procesando_desde, datos_extra, fecha_uso, validado_por y datos del asistente
-- son nullable, sin default; nombre_asistente/documento_identidad son VARCHAR
-- sin límite declarado. Las reglas específicas de carreras van en Application.
-- Los CHECKs verifican estados, cantidades e importes, incluidos los totales.
-- La aplicación generará/validará el QR HMAC-SHA256 en F5-05/F5-07; un UNIQUE
-- sobre codigo_qr no prueba su firma. No se generan boletos ni tokens aquí.
-- La reserva atómica, devolución idempotente y transiciones condicionales se
-- implementan en los casos de uso. La coherencia entre el evento de la compra
-- y sus tipos de entrada, índices y permisos se incorporan en V9 (F1-07).

CREATE TABLE compras (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    public_id UUID NOT NULL DEFAULT gen_random_uuid(),
    cliente_id INT NOT NULL,
    evento_id INT NOT NULL,
    fecha_compra TIMESTAMPTZ NOT NULL DEFAULT now(),
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    expira_en TIMESTAMPTZ NOT NULL,
    procesando_desde TIMESTAMPTZ,
    idempotency_key VARCHAR(100) NOT NULL,
    moneda CHAR(3) NOT NULL DEFAULT 'GTQ',
    subtotal NUMERIC(10,2) NOT NULL DEFAULT 0,
    impuestos NUMERIC(10,2) NOT NULL DEFAULT 0,
    comision_plataforma NUMERIC(10,2) NOT NULL DEFAULT 0,
    total NUMERIC(10,2) NOT NULL DEFAULT 0,
    CONSTRAINT uq_compras_public_id UNIQUE (public_id),
    CONSTRAINT uq_compras_idempotency_key UNIQUE (idempotency_key),
    CONSTRAINT fk_compras_cliente FOREIGN KEY (cliente_id)
        REFERENCES clientes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_compras_evento FOREIGN KEY (evento_id)
        REFERENCES eventos (id) ON DELETE RESTRICT,
    CONSTRAINT ck_compras_estado CHECK (
        estado IN ('PENDIENTE', 'PROCESANDO', 'COMPLETADA', 'CANCELADA', 'FALLIDA', 'EXPIRADA')
    ),
    CONSTRAINT ck_compras_importes CHECK (
        subtotal >= 0 AND impuestos >= 0 AND comision_plataforma >= 0 AND total >= 0
    ),
    CONSTRAINT ck_compras_total CHECK (
        total = subtotal + impuestos + comision_plataforma
    )
);

CREATE TABLE detalle_compra (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    compra_id INT NOT NULL,
    tipo_entrada_id INT NOT NULL,
    cantidad INT NOT NULL,
    precio_unitario NUMERIC(10,2) NOT NULL,
    subtotal NUMERIC(10,2) NOT NULL,
    CONSTRAINT fk_detalle_compra_compra FOREIGN KEY (compra_id)
        REFERENCES compras (id) ON DELETE RESTRICT,
    CONSTRAINT fk_detalle_compra_tipo_entrada FOREIGN KEY (tipo_entrada_id)
        REFERENCES tipos_entrada (id) ON DELETE RESTRICT,
    CONSTRAINT ck_detalle_compra_cantidad CHECK (cantidad > 0),
    CONSTRAINT ck_detalle_compra_importes CHECK (
        precio_unitario >= 0 AND subtotal >= 0
    ),
    CONSTRAINT ck_detalle_compra_subtotal CHECK (
        subtotal = cantidad * precio_unitario
    )
);

CREATE TABLE boletos (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    public_id UUID NOT NULL DEFAULT gen_random_uuid(),
    detalle_compra_id INT NOT NULL,
    codigo_qr VARCHAR(100) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'VALIDO',
    nombre_asistente VARCHAR,
    documento_identidad VARCHAR,
    datos_extra JSONB,
    fecha_emision TIMESTAMPTZ NOT NULL DEFAULT now(),
    fecha_uso TIMESTAMPTZ,
    validado_por INT,
    CONSTRAINT uq_boletos_public_id UNIQUE (public_id),
    CONSTRAINT uq_boletos_codigo_qr UNIQUE (codigo_qr),
    CONSTRAINT fk_boletos_detalle_compra FOREIGN KEY (detalle_compra_id)
        REFERENCES detalle_compra (id) ON DELETE RESTRICT,
    CONSTRAINT fk_boletos_validador FOREIGN KEY (validado_por)
        REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_boletos_estado CHECK (estado IN ('VALIDO', 'USADO', 'ANULADO'))
);
