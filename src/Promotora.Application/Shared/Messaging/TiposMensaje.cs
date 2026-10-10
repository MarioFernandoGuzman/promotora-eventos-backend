namespace Promotora.Application.Shared.Messaging;

public static class TiposMensaje
{
    public const string ProcesarCompra = "compra.procesar";
    public const string CompraCompletada = "compra.completada";
    public const string UsuarioRegistrado = "usuario.registrado";

    public static bool EsValido(string tipo) => tipo is ProcesarCompra or CompraCompletada or UsuarioRegistrado;
}
