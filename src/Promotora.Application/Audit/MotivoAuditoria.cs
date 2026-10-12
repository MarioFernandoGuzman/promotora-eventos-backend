namespace Promotora.Application.Audit;

public enum MotivoAuditoria
{
    AltaUsuario,
    BootstrapAdmin,
    LoginCorrecto,
    LoginRechazado,
    LogoutSolicitado,
    RefreshReutilizado,
    SesionExpiradaOInhabilitada,
    EdicionUsuario,
    BajaLogica,
    CambioPassword,
    CambioPasswordRechazado,
    ResenaEditada,
    ResenaRetirada,
    ResenaAprobada,
    ResenaRechazada
}
