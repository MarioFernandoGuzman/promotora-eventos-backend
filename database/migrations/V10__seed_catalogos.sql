-- F1-09: datos base de produccion; BASE_DE_DATOS.md §§4.3, 4.5 y 12.
-- Solo las seis categorias documentadas y cuentas PLATAFORMA/IMPUESTOS.
-- Suposicion aprobada: moneda inicial GTQ; saldos cero y estado ACTIVA
-- mediante los defaults del esquema. No se crean movimientos ni usuarios.
-- ON CONFLICT reutiliza los catalogos/cuentas existentes sin modificarlos.
-- El SUPERADMIN pertenece al bootstrap de F3-01, con user-secrets/env.

INSERT INTO categorias_evento (nombre) VALUES
    ('Concierto'), ('Partido'), ('Vigilia'), ('Culto'), ('Carrera'), ('Meet & Greet')
ON CONFLICT (nombre) DO NOTHING;

INSERT INTO cuentas_financieras (tipo, moneda) VALUES ('PLATAFORMA', 'GTQ')
ON CONFLICT (moneda) WHERE tipo = 'PLATAFORMA' DO NOTHING;

INSERT INTO cuentas_financieras (tipo, moneda) VALUES ('IMPUESTOS', 'GTQ')
ON CONFLICT (moneda) WHERE tipo = 'IMPUESTOS' DO NOTHING;
