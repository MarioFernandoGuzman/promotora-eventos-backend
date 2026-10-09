namespace Promotora.Application.Identity.Ports;

// Traducción del UNIQUE del adaptador; el handler lo transforma en Result de conflicto.
public sealed class UsuarioEmailDuplicadoException : Exception;
