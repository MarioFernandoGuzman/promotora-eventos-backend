-- F1-02: identidad y renovación de sesiones.
-- Referencias: docs/BASE_DE_DATOS.md §§3, 4.1 y 12;
-- docs/referencia/schema_v1.sql (solo referencia histórica, no ejecutar).
-- Tipos, claves, nulabilidad y defaults heredados de v1, salvo ajustes v2:
-- SERIAL pasa a GENERATED ALWAYS AS IDENTITY; fechas/hora a TIMESTAMPTZ;
-- fecha_nacimiento permanece DATE; FKs a usuarios usan ON DELETE RESTRICT.
-- refresh_tokens y debe_cambiar_password siguen la definición aprobada.
-- La normalización del email, el hash de contraseñas y la rotación del refresh
-- pertenecen a la aplicación; aquí solo se almacena el hash del token.
-- Índices adicionales, permisos y constraint trigger diferido de perfil
-- se incorporan en V9__indices_triggers_permisos.sql (F1-07).
-- No se crean usuarios iniciales ni se incluyen credenciales.

CREATE TABLE usuarios (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    email VARCHAR(150) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    debe_cambiar_password BOOLEAN NOT NULL DEFAULT false,
    nombre VARCHAR(100) NOT NULL,
    apellido VARCHAR(100) NOT NULL,
    rol VARCHAR(20) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'ACTIVO',
    fecha_registro TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_usuarios_email UNIQUE (email),
    CONSTRAINT ck_usuarios_rol CHECK (
        rol IN ('CLIENTE', 'ADMINISTRADOR', 'DUENO_RECINTO')
    ),
    CONSTRAINT ck_usuarios_estado CHECK (
        estado IN ('ACTIVO', 'INACTIVO', 'SUSPENDIDO')
    )
);

CREATE TABLE clientes (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id INT NOT NULL,
    fecha_nacimiento DATE,
    telefono VARCHAR(20),
    CONSTRAINT uq_clientes_usuario UNIQUE (usuario_id),
    CONSTRAINT fk_clientes_usuario FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id) ON DELETE RESTRICT
);

CREATE TABLE administradores (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id INT NOT NULL,
    nivel_acceso VARCHAR(20) NOT NULL DEFAULT 'ESTANDAR',
    CONSTRAINT uq_administradores_usuario UNIQUE (usuario_id),
    CONSTRAINT fk_administradores_usuario FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_administradores_nivel_acceso CHECK (
        nivel_acceso IN ('ESTANDAR', 'SUPERADMIN')
    )
);

CREATE TABLE duenos_recinto (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id INT NOT NULL,
    empresa VARCHAR(150) NOT NULL,
    nit VARCHAR(20),
    CONSTRAINT uq_duenos_recinto_usuario UNIQUE (usuario_id),
    CONSTRAINT fk_duenos_recinto_usuario FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id) ON DELETE RESTRICT
);

CREATE TABLE refresh_tokens (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id INT NOT NULL,
    token_hash VARCHAR(255) NOT NULL,
    familia_id UUID NOT NULL,
    expira_en TIMESTAMPTZ NOT NULL,
    revocado_en TIMESTAMPTZ,
    reemplazado_por_id BIGINT,
    ip_origen VARCHAR(45),
    fecha_creacion TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_refresh_tokens_token_hash UNIQUE (token_hash),
    CONSTRAINT fk_refresh_tokens_usuario FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refresh_tokens_reemplazo FOREIGN KEY (reemplazado_por_id)
        REFERENCES refresh_tokens (id) ON DELETE RESTRICT
);
