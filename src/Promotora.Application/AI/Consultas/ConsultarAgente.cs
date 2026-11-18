using System.Text.RegularExpressions;
using FluentValidation;
using Promotora.Application.AI.Ports;
using Promotora.Application.AI.Rag;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.AI.Consultas;

public sealed record ConsultarAgenteCommand(string Pregunta, Guid? SessionId = null) : ICommand<ConsultaIaDto>;
public sealed record ConsultaIaDto(Guid SessionId, string Respuesta, IReadOnlyList<FuenteCitadaDto> Fuentes, string Estado,
    string? Modelo = null, int? TokensEntrada = null, int? TokensSalida = null, IReadOnlyList<string>? HerramientasUtilizadas = null);
public interface IInformacionPublicaPort
{
    Task<IReadOnlyList<FuenteContexto>> BuscarAsync(string pregunta, CancellationToken ct);
}
public sealed class ConsultarAgenteValidator : AbstractValidator<ConsultarAgenteCommand>
{
    public ConsultarAgenteValidator()
    {
        RuleFor(x => x.Pregunta).NotEmpty().MaximumLength(PoliticaAsistente.MaxPreguntaCaracteres)
            .Must(p => p is not null && !p.Any(c => char.IsControl(c) && c is not ('\n' or '\t')));
        RuleFor(x => x.SessionId).NotEqual(Guid.Empty).When(x => x.SessionId.HasValue);
    }
}
public sealed class ConsultarAgenteHandler(RecuperarContexto recuperar, RespuestaFundamentada fundamento, ILlmPort llm,
    IEnumerable<IInformacionPublicaPort> datosPublicos, ISelectorHerramientaPort selector, ICalificacionPublicaPort calificacion) : ICommandHandler<ConsultarAgenteCommand, ConsultaIaDto>
{
    public const string HerramientaCalificacion = "consultar_calificacion_presentador";
    private static readonly SemaphoreSlim Capacidad = new(2, 2);
    public async Task<Result<ConsultaIaDto>> HandleAsync(ConsultarAgenteCommand request, CancellationToken ct)
    {
        if (!await Capacidad.WaitAsync(0, ct)) return Result<ConsultaIaDto>.Failure(new Error("ia.ocupada", "El agente está ocupado; vuelve a intentarlo más tarde.", ErrorType.RateLimit));
        try { return await ResponderAsync(request, ct); }
        finally { Capacidad.Release(); }
    }
    private async Task<Result<ConsultaIaDto>> ResponderAsync(ConsultarAgenteCommand request, CancellationToken ct)
    {
        Guid session = request.SessionId ?? Guid.NewGuid();
        int tokensSelectorEntrada = 0, tokensSelectorSalida = 0;
        if (SeguridadAgente.Rechazo(request.Pregunta) is string rechazo)
            return Result<ConsultaIaDto>.Success(new(session, "Hola, soy José. " + rechazo, [], "RECHAZADA"));
        // Solo ofrecer esta herramienta cuando la consulta contiene un identificador explícito.
        if (Regex.IsMatch(request.Pregunta, @"(?i)\b(presentador\w*|artista\w*)\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            && Regex.IsMatch(request.Pregunta, @"(?i)\b(calificaci[oó]n|rese[nñ]as?|puntuaci[oó]n|promedio)\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            && Regex.IsMatch(request.Pregunta, @"\b[0-9]+\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
        {
            var seleccion = await selector.SeleccionarAsync(request.Pregunta, ct);
            if (seleccion.IsFailure) return Result<ConsultaIaDto>.Failure(seleccion.Errors);
            tokensSelectorEntrada = seleccion.Value.TokensEntrada;
            tokensSelectorSalida = seleccion.Value.TokensSalida;
            if (seleccion.Value.PresentadorId is int id)
            {
                bool idPresente = Regex.Matches(request.Pregunta, @"\b[0-9]+\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
                    .Any(m => int.TryParse(m.Value, out int numero) && numero == id);
                if (id <= 0 || !idPresente) return Result<ConsultaIaDto>.Failure(new Error("ia.herramienta", "El identificador de la herramienta no es válido.", ErrorType.Validation));
                var fuente = await calificacion.ConsultarAsync(id, ct);
                string[] herramientas = [HerramientaCalificacion];
                if (fuente is null)
                    return Result<ConsultaIaDto>.Success(new(session, "No encontré un presentador activo con ese identificador.", [], "SIN_CONTEXTO", null, seleccion.Value.TokensEntrada, seleccion.Value.TokensSalida, herramientas));
                var contextoHerramienta = fundamento.Preparar([fuente]);
                if (contextoHerramienta.Fuentes.Count == 0) return SinContexto(session, request.SessionId is null);
                var respuesta = await llm.ResponderAsync(new(request.Pregunta, contextoHerramienta.Json, request.SessionId is null), ct);
                if (respuesta.IsFailure) return Result<ConsultaIaDto>.Failure(respuesta.Errors);
                var citas = fundamento.Validar(respuesta.Value.Texto, contextoHerramienta, request.SessionId is null);
                int entrada = checked(seleccion.Value.TokensEntrada + respuesta.Value.TokensEntrada);
                int salida = checked(seleccion.Value.TokensSalida + respuesta.Value.TokensSalida);
                if (citas.IsFailure)
                    return Result<ConsultaIaDto>.Success(new(session, "No tengo información pública suficiente para responder con fuentes verificables.", [], "SIN_CONTEXTO", respuesta.Value.Modelo, entrada, salida, herramientas));
                return Result<ConsultaIaDto>.Success(new(session, citas.Value.Respuesta, citas.Value.Fuentes, "OK", respuesta.Value.Modelo, entrada, salida, herramientas));
            }
        }
        var recuperado = await recuperar.BuscarAsync(request.Pregunta, ct);
        if (recuperado.IsFailure) return Result<ConsultaIaDto>.Failure(recuperado.Errors);
        var fuentes = recuperado.Value.ToList();
        foreach (var datos in datosPublicos) fuentes.AddRange(await datos.BuscarAsync(request.Pregunta, ct));
        var contexto = fundamento.Preparar(fuentes);
        if (contexto.Fuentes.Count == 0) return SinContexto(session, request.SessionId is null, tokensEntrada: tokensSelectorEntrada, tokensSalida: tokensSelectorSalida);
        var generado = await llm.ResponderAsync(new(request.Pregunta, contexto.Json, request.SessionId is null), ct);
        if (generado.IsFailure) return Result<ConsultaIaDto>.Failure(generado.Errors);
        var validado = fundamento.Validar(generado.Value.Texto, contexto, request.SessionId is null);
        if (validado.IsFailure) return SinContexto(session, request.SessionId is null, generado.Value, tokensSelectorEntrada, tokensSelectorSalida);
        return Result<ConsultaIaDto>.Success(new(session, validado.Value.Respuesta, validado.Value.Fuentes, "OK", generado.Value.Modelo, checked(generado.Value.TokensEntrada + tokensSelectorEntrada), checked(generado.Value.TokensSalida + tokensSelectorSalida)));
    }
    private static Result<ConsultaIaDto> SinContexto(Guid session, bool presentarse, RespuestaLlm? respuesta = null, int tokensEntrada = 0, int tokensSalida = 0) => Result<ConsultaIaDto>.Success(
        new(session, (presentarse ? "Hola, soy José. " : "") + "No tengo información pública suficiente para responder con fuentes verificables. Puedes indicar el evento o consultar a la organización.",
            [], "SIN_CONTEXTO", respuesta?.Modelo, respuesta is null && tokensEntrada == 0 ? null : checked((respuesta?.TokensEntrada ?? 0) + tokensEntrada), respuesta is null && tokensSalida == 0 ? null : checked((respuesta?.TokensSalida ?? 0) + tokensSalida)));
}
