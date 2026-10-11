-- F6-11: diario durable del proveedor simulado, aprobado por el usuario.
-- Suposiciones: public sin FK a compras (commit independiente del proveedor);
-- referencia = compras.public_id; moneda GTQ del alcance actual; monto permite cero;
-- fecha UTC del primer resultado. No almacenar datos de tarjeta ni secretos.
-- No sustituye pagos/ledger ni afirma un cobro externo. Solo SELECT/INSERT.
CREATE TABLE public.pagos_simulados (
    referencia UUID PRIMARY KEY,
    monto NUMERIC(10,2) NOT NULL CHECK (monto >= 0),
    moneda VARCHAR(3) NOT NULL CHECK (moneda = 'GTQ'),
    estado VARCHAR(20) NOT NULL CHECK (estado IN ('APROBADO', 'RECHAZADO')),
    fecha TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TRIGGER proteger_inmutabilidad BEFORE UPDATE OR DELETE OR TRUNCATE ON public.pagos_simulados
FOR EACH STATEMENT EXECUTE FUNCTION public.rechazar_mutacion_inmutable();
ALTER TABLE public.pagos_simulados OWNER TO flyway;
REVOKE ALL ON public.pagos_simulados FROM PUBLIC, promotora_app;
GRANT SELECT, INSERT ON public.pagos_simulados TO promotora_app;
