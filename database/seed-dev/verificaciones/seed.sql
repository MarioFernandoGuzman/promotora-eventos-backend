DO $$ BEGIN
    IF (SELECT count(*) FROM usuarios)<>2 OR (SELECT count(*) FROM usuarios WHERE estado='INACTIVO')<>2
       OR EXISTS(SELECT 1 FROM administradores) OR (SELECT count(*) FROM duenos_recinto)<>1
       OR (SELECT count(*) FROM clientes)<>1 OR (SELECT count(*) FROM recintos)<>1
       OR (SELECT count(*) FROM localidades)<>2 OR (SELECT count(*) FROM eventos)<>2
       OR (SELECT count(*) FROM presentadores)<>2 OR (SELECT count(*) FROM tipos_entrada)<>4
       OR EXISTS(SELECT 1 FROM compras) OR EXISTS(SELECT 1 FROM movimientos_financieros)
       OR EXISTS(SELECT 1 FROM bi.vw_ocupacion_localidad WHERE cupo_ocupado<>0)
       OR (SELECT count(*) FROM categorias_evento)<>6 OR (SELECT count(*) FROM cuentas_financieras)<>2 THEN
        RAISE EXCEPTION 'Seed dev duplicado o datos inesperados';
    END IF;
END; $$;
