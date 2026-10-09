-- F1-09: datos sinteticos EXCLUSIVOS de desarrollo, fuera de Flyway.
-- Ejecutar despues de V10 con psql -X -v ON_ERROR_STOP=1.
-- No crea administradores, compras, pagos, QR ni movimientos financieros.
-- Usuarios INACTIVO; password_hash es un marcador inutilizable, NO un hash
-- de autenticacion. No hay contrasenas ni secretos y no permite login.
-- Idempotente: solo INSERT; no cambia cupos ni fechas de filas existentes.

BEGIN;
SET LOCAL TIME ZONE 'UTC';
SELECT pg_advisory_xact_lock(hashtextextended('promotora.seed-dev.catalogo', 0));

DO $$
DECLARE
    usuario_dueno integer;
    dueno integer;
    recinto integer;
    palco integer;
    general integer;
    concierto integer;
    carrera integer;
    artista integer;
    organizacion integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM categorias_evento WHERE nombre = 'Concierto')
       OR NOT EXISTS (SELECT 1 FROM categorias_evento WHERE nombre = 'Carrera') THEN
        RAISE EXCEPTION 'Aplicar V10__seed_catalogos.sql antes del seed de desarrollo';
    END IF;
    INSERT INTO usuarios (email, password_hash, nombre, apellido, rol, estado) VALUES
        ('dueno.seed@example.invalid', '!SIN_ACCESO_SEED_DEV!', 'Dueno', 'Demo', 'DUENO_RECINTO', 'INACTIVO'),
        ('cliente.seed@example.invalid', '!SIN_ACCESO_SEED_DEV!', 'Cliente', 'Demo', 'CLIENTE', 'INACTIVO')
    ON CONFLICT (email) DO NOTHING;
    IF EXISTS (
        SELECT 1 FROM usuarios WHERE email IN ('dueno.seed@example.invalid', 'cliente.seed@example.invalid')
        AND (estado <> 'INACTIVO' OR password_hash <> '!SIN_ACCESO_SEED_DEV!'
             OR rol <> CASE email WHEN 'dueno.seed@example.invalid' THEN 'DUENO_RECINTO' ELSE 'CLIENTE' END)
    ) THEN
        RAISE EXCEPTION 'Identidades seed existentes incompatibles; no se modifican usuarios';
    END IF;
    SELECT id INTO usuario_dueno FROM usuarios WHERE email = 'dueno.seed@example.invalid';
    INSERT INTO duenos_recinto (usuario_id, empresa) VALUES (usuario_dueno, 'Promotora Demo')
    ON CONFLICT (usuario_id) DO NOTHING;
    INSERT INTO clientes (usuario_id)
    SELECT id FROM usuarios WHERE email = 'cliente.seed@example.invalid'
    ON CONFLICT (usuario_id) DO NOTHING;
    SELECT id INTO dueno FROM duenos_recinto WHERE usuario_id = usuario_dueno;

    INSERT INTO recintos (dueno_id, nombre, direccion, ciudad, capacidad_total)
    SELECT dueno, 'Recinto Demo', 'Direccion sintetica', 'Ciudad Demo', 1000
    WHERE NOT EXISTS (SELECT 1 FROM recintos WHERE dueno_id = dueno AND nombre = 'Recinto Demo');
    SELECT id INTO recinto FROM recintos WHERE dueno_id = dueno AND nombre = 'Recinto Demo';
    INSERT INTO localidades (recinto_id, nombre, capacidad, geometria, color) VALUES
        (recinto, 'Palco', 200, '{"type":"polygon","points":[[0,0],[100,0],[100,20],[0,20]]}', '#3366CC'),
        (recinto, 'General', 800, '{"type":"polygon","points":[[0,20],[100,20],[100,100],[0,100]]}', '#33AA66')
    ON CONFLICT (recinto_id, nombre) DO NOTHING;
    SELECT id INTO palco FROM localidades WHERE recinto_id = recinto AND nombre = 'Palco';
    SELECT id INTO general FROM localidades WHERE recinto_id = recinto AND nombre = 'General';

    INSERT INTO presentadores (nombre, tipo)
    SELECT 'Artista Demo', 'ARTISTA'
    WHERE NOT EXISTS (SELECT 1 FROM presentadores WHERE nombre = 'Artista Demo' AND tipo = 'ARTISTA');
    INSERT INTO presentadores (nombre, tipo)
    SELECT 'Organizacion Demo', 'ORGANIZACION'
    WHERE NOT EXISTS (SELECT 1 FROM presentadores WHERE nombre = 'Organizacion Demo' AND tipo = 'ORGANIZACION');
    SELECT id INTO artista FROM presentadores WHERE nombre = 'Artista Demo' AND tipo = 'ARTISTA';
    SELECT id INTO organizacion FROM presentadores WHERE nombre = 'Organizacion Demo' AND tipo = 'ORGANIZACION';

    INSERT INTO eventos (recinto_id, categoria_id, creado_por, nombre, descripcion,
                        fecha_inicio, fecha_fin, fecha_inicio_venta, fecha_fin_venta, estado)
    SELECT recinto, id, usuario_dueno, 'Concierto Demo', 'Evento sintetico para explorar localidades',
           now() + interval '30 days', now() + interval '30 days 3 hours',
           now() - interval '1 day', now() + interval '29 days', 'EN_VENTA'
    FROM categorias_evento WHERE nombre = 'Concierto'
      AND NOT EXISTS (SELECT 1 FROM eventos WHERE recinto_id = recinto AND nombre = 'Concierto Demo');
    INSERT INTO eventos (recinto_id, categoria_id, creado_por, nombre, descripcion,
                        fecha_inicio, fecha_fin, fecha_inicio_venta, fecha_fin_venta, estado)
    SELECT recinto, id, usuario_dueno, 'Carrera Demo', 'Evento sintetico con entradas 5K y 10K',
           now() + interval '45 days', now() + interval '45 days 4 hours',
           now() - interval '1 day', now() + interval '44 days', 'EN_VENTA'
    FROM categorias_evento WHERE nombre = 'Carrera'
      AND NOT EXISTS (SELECT 1 FROM eventos WHERE recinto_id = recinto AND nombre = 'Carrera Demo');
    SELECT id INTO concierto FROM eventos WHERE recinto_id = recinto AND nombre = 'Concierto Demo';
    SELECT id INTO carrera FROM eventos WHERE recinto_id = recinto AND nombre = 'Carrera Demo';
    INSERT INTO evento_presentadores (evento_id, presentador_id, rol) VALUES
        (concierto, artista, 'HEADLINER'), (carrera, organizacion, 'ORGANIZADOR')
    ON CONFLICT (evento_id, presentador_id) DO NOTHING;

    INSERT INTO tipos_entrada (evento_id, localidad_id, nombre, precio, cupo_total, cupo_disponible)
    SELECT concierto, palco, 'Palco', 250, 200, 200
    WHERE NOT EXISTS (SELECT 1 FROM tipos_entrada WHERE evento_id = concierto AND nombre = 'Palco');
    INSERT INTO tipos_entrada (evento_id, localidad_id, nombre, precio, cupo_total, cupo_disponible)
    SELECT concierto, general, 'General', 100, 800, 800
    WHERE NOT EXISTS (SELECT 1 FROM tipos_entrada WHERE evento_id = concierto AND nombre = 'General');
    INSERT INTO tipos_entrada (evento_id, nombre, precio, cupo_total, cupo_disponible)
    SELECT carrera, '5K', 75, 500, 500
    WHERE NOT EXISTS (SELECT 1 FROM tipos_entrada WHERE evento_id = carrera AND nombre = '5K');
    INSERT INTO tipos_entrada (evento_id, nombre, precio, cupo_total, cupo_disponible)
    SELECT carrera, '10K', 125, 500, 500
    WHERE NOT EXISTS (SELECT 1 FROM tipos_entrada WHERE evento_id = carrera AND nombre = '10K');
END;
$$;

COMMIT;
