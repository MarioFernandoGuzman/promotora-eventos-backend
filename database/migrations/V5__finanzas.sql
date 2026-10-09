-- F1-05: intentos de pago, cuentas financieras y movimientos.
-- Referencias: docs/BASE_DE_DATOS.md §§3, 4.5, 6, 9 y 12.
-- Suposiciones aprobadas para estas tablas nuevas: cuentas.id INT identity;
-- pagos.id y movimientos.id BIGINT identity (alto volumen).
-- Tipos/estados son VARCHAR(20) NOT NULL con CHECK, sin ENUM nativo.
-- FKs obligatorias salvo cuentas.dueno_id según tipo y movimientos.compra_id
-- (NULL en liquidaciones manuales). Todas las FKs usan ON DELETE RESTRICT.
-- Pagos: pasarela y monto NOT NULL sin default; estado DEFAULT PENDIENTE;
-- metodo VARCHAR(30), referencia VARCHAR(100) y respuesta JSONB son nullable
-- sin default. monto admite cero para entradas gratuitas, nunca negativos.
-- Cuentas: moneda CHAR(3) NOT NULL DEFAULT GTQ, saldo NOT NULL DEFAULT 0,
-- estado NOT NULL DEFAULT ACTIVA y fecha_cierre nullable sin default.
-- Movimientos: monto con signo, obligatorio y distinto de cero; descripcion
-- TEXT nullable sin default. Fecha de pago/movimiento NOT NULL DEFAULT now().
-- CERRADA exige cuenta DUENO con saldo cero. Registrar fecha_cierre y cambiar
-- estados condicionalmente corresponde a los casos de uso.
-- Los indices unicos parciales PLATAFORMA/IMPUESTOS por moneda, el bloqueo de
-- movimientos en cuentas cerradas y la inmutabilidad del ledger (trigger y
-- REVOKE UPDATE/DELETE) se incorporan en V9 (F1-07).
-- La conciliacion saldo = SUM(monto), los tres componentes de cobro y sus
-- reembolsos se implementan transaccionalmente en F7; no se insertan datos aqui.

CREATE TABLE pagos (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    compra_id INT NOT NULL,
    pasarela VARCHAR(30) NOT NULL,
    metodo VARCHAR(30),
    referencia VARCHAR(100),
    monto NUMERIC(10,2) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    respuesta JSONB,
    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT fk_pagos_compra FOREIGN KEY (compra_id)
        REFERENCES compras (id) ON DELETE RESTRICT,
    CONSTRAINT ck_pagos_monto CHECK (monto >= 0),
    CONSTRAINT ck_pagos_estado CHECK (
        estado IN ('PENDIENTE', 'APROBADO', 'RECHAZADO', 'REEMBOLSADO')
    )
);

CREATE TABLE cuentas_financieras (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo VARCHAR(20) NOT NULL,
    dueno_id INT,
    moneda CHAR(3) NOT NULL DEFAULT 'GTQ',
    saldo NUMERIC(12,2) NOT NULL DEFAULT 0,
    estado VARCHAR(20) NOT NULL DEFAULT 'ACTIVA',
    fecha_cierre TIMESTAMPTZ,
    CONSTRAINT fk_cuentas_financieras_dueno FOREIGN KEY (dueno_id)
        REFERENCES duenos_recinto (id) ON DELETE RESTRICT,
    CONSTRAINT uq_cuentas_financieras_dueno_moneda UNIQUE (dueno_id, moneda),
    CONSTRAINT ck_cuentas_financieras_tipo CHECK (
        tipo IN ('PLATAFORMA', 'DUENO', 'IMPUESTOS')
    ),
    CONSTRAINT ck_cuentas_financieras_dueno CHECK (
        (tipo = 'DUENO') = (dueno_id IS NOT NULL)
    ),
    CONSTRAINT ck_cuentas_financieras_estado CHECK (estado IN ('ACTIVA', 'CERRADA')),
    CONSTRAINT ck_cuentas_financieras_cierre CHECK (
        estado <> 'CERRADA' OR (tipo = 'DUENO' AND saldo = 0)
    )
);

CREATE TABLE movimientos_financieros (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cuenta_id INT NOT NULL,
    compra_id INT,
    tipo VARCHAR(20) NOT NULL,
    monto NUMERIC(12,2) NOT NULL,
    descripcion TEXT,
    fecha TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT fk_movimientos_financieros_cuenta FOREIGN KEY (cuenta_id)
        REFERENCES cuentas_financieras (id) ON DELETE RESTRICT,
    CONSTRAINT fk_movimientos_financieros_compra FOREIGN KEY (compra_id)
        REFERENCES compras (id) ON DELETE RESTRICT,
    CONSTRAINT ck_movimientos_financieros_tipo CHECK (
        tipo IN ('VENTA', 'COMISION', 'IMPUESTO', 'LIQUIDACION', 'REEMBOLSO')
    ),
    CONSTRAINT ck_movimientos_financieros_monto CHECK (monto <> 0)
);
