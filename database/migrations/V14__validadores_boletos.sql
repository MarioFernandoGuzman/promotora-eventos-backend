ALTER TABLE public.usuarios DROP CONSTRAINT ck_usuarios_rol;
ALTER TABLE public.usuarios ADD CONSTRAINT ck_usuarios_rol
    CHECK (rol IN ('CLIENTE', 'ADMINISTRADOR', 'DUENO_RECINTO', 'VALIDADOR_BOLETOS'));

CREATE TABLE public.validadores_boletos (
    usuario_id integer PRIMARY KEY REFERENCES public.usuarios(id) ON DELETE RESTRICT
);

CREATE OR REPLACE FUNCTION validar_perfil_usuario() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, public AS $$
DECLARE
    usuario_actual integer;
    rol_actual text;
    perfiles integer;
    rol_perfil text;
    usuarios_validar integer[];
BEGIN
    IF TG_TABLE_NAME = 'usuarios' THEN
        usuarios_validar := ARRAY[NEW.id];
    ELSIF TG_OP = 'INSERT' THEN
        usuarios_validar := ARRAY[NEW.usuario_id];
    ELSIF TG_OP = 'DELETE' THEN
        usuarios_validar := ARRAY[OLD.usuario_id];
    ELSE
        usuarios_validar := ARRAY[OLD.usuario_id, NEW.usuario_id];
    END IF;
    FOR usuario_actual IN
        SELECT DISTINCT x FROM unnest(usuarios_validar) AS ids(x)
    LOOP
        SELECT rol INTO rol_actual FROM public.usuarios WHERE id = usuario_actual;
        IF NOT FOUND THEN CONTINUE; END IF;
        SELECT count(*), min(p.rol) INTO perfiles, rol_perfil FROM (
            SELECT 'CLIENTE' AS rol FROM public.clientes WHERE usuario_id = usuario_actual
            UNION ALL SELECT 'ADMINISTRADOR' FROM public.administradores WHERE usuario_id = usuario_actual
            UNION ALL SELECT 'DUENO_RECINTO' FROM public.duenos_recinto WHERE usuario_id = usuario_actual
            UNION ALL SELECT 'VALIDADOR_BOLETOS' FROM public.validadores_boletos WHERE usuario_id = usuario_actual
        ) p;
        IF perfiles <> 1 OR rol_perfil <> rol_actual THEN
            RAISE EXCEPTION 'Usuario % requiere exactamente un perfil acorde al rol %', usuario_actual, rol_actual USING ERRCODE = '23514';
        END IF;
    END LOOP;
    RETURN NULL;
END;
$$;

CREATE TRIGGER bloquear_perfil BEFORE INSERT OR UPDATE OR DELETE ON public.validadores_boletos
    FOR EACH ROW EXECUTE FUNCTION public.bloquear_usuario_perfil();
CREATE CONSTRAINT TRIGGER validar_perfil AFTER INSERT OR UPDATE OR DELETE ON public.validadores_boletos
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.validar_perfil_usuario();

REVOKE ALL ON public.validadores_boletos FROM PUBLIC, promotora_app;
GRANT SELECT, INSERT ON public.validadores_boletos TO promotora_app;
