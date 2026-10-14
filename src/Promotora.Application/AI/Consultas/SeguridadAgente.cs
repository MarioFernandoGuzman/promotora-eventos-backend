using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Promotora.Application.AI.Consultas;

public static class SeguridadAgente
{
    private static string Normalizar(string texto) => new string(texto.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
    public static bool ContenidoSeguro(string texto) => !ConsultaPrivacidad.ContieneCredenciales(texto)
        && !Coincide(Normalizar(texto), @"(ignora.{0,30}instrucciones|ignore.{0,30}(instructions|previous)|olvida.{0,20}reglas|system\s*prompt|developer\s*message|actua\s+como|act\s+as|jailbreak|<\s*(script|system)|revela.{0,20}(prompt|instrucciones))");
    public static string? Rechazo(string pregunta)
    {
        string texto = Normalizar(pregunta);
        if (!ContenidoSeguro(pregunta) || Coincide(texto, @"(\b(mi|mis|tu|tus)\s+(compras|pagos|saldo|cuenta|boletos|correo|datos)\b|lista.{0,15}usuarios|datos\s+personales|cuentas\s+financieras|\b(select|drop|insert|delete)\s+.{0,20}(from|table|into)|transfiere|hackea)"))
            return "Solo puedo ayudarte con información pública de la promotora. No compartas credenciales ni datos personales; consulta tus operaciones desde tu cuenta.";
        if (!Coincide(texto, @"\b(evento\w*|promotora|organizacion|concierto\w*|recinto\w*|entrada\w*|boleto\w*|reglamento\w*|reembolso\w*|carrera\w*|localidad\w*|precio\w*|fecha\w*|horario\w*|artista\w*|presentador\w*|equipo\w*|acceso|asa|jose|hola|buenas|saludos)\b"))
            return "Mi alcance es la información pública de la promotora: eventos, entradas, recintos, fechas y reglamentos. Indica el evento o tema sobre el que deseas información.";
        return null;
    }
    private static bool Coincide(string texto, string patron) => Regex.IsMatch(texto, patron, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
