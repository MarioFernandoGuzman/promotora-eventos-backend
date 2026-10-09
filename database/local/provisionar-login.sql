-- Configuración local de acceso, fuera del historial de migraciones.
-- Ejecutar como postgres tras Flyway. La clave llega exclusivamente del entorno.
-- No imprimir variables ni usar psql --echo-all con este archivo.
\getenv app_password PROMOTORA_DB_PASSWORD
\if :{?app_password}
\else
  \echo 'Falta PROMOTORA_DB_PASSWORD'
  \quit 1
\endif
SELECT length(:'app_password') > 0 AS password_ok \gset
\if :password_ok
\else
  \echo 'PROMOTORA_DB_PASSWORD no puede estar vacía'
  \quit 1
\endif
SELECT NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='promotora_runtime') AS crear \gset
\if :crear
  SELECT format('CREATE ROLE promotora_runtime LOGIN INHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L IN ROLE promotora_app', :'app_password') \gexec
\else
  \echo 'El login ya existe; se conserva su contraseña. Usa la configurada inicialmente.'
\endif
