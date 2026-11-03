using FluentValidation;
using Promotora.Application.AI.Ports;
using Promotora.Application.AI.Rag;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.AI.Consultas;

public sealed record ConsultarAgenteCommand(string Pregunta, Guid? SessionId = null) : ICommand<ConsultaIaDto>;
public sealed record ConsultaIaDto(Guid SessionId, string Respuesta, IReadOnlyList<FuenteCitadaDto> Fuentes, string Estado,
    string? Modelo = null, int? TokensEntrada = null, int? TokensSalida = null);
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
    IEnumerable<IInformacionPublicaPort> datosPublicos) : ICommandHandler<ConsultarAgenteCommand, ConsultaIaDto>
{
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
        if (SeguridadAgente.Rechazo(request.Pregunta) is string rechazo)
            return Result<ConsultaIaDto>.Success(new(session, "Hola, soy José. " + rechazo, [], "RECHAZADA"));
        var recuperado = await recuperar.BuscarAsync(request.Pregunta, ct);
        if (recuperado.IsFailure) return Result<ConsultaIaDto>.Failure(recuperado.Errors);
        var fuentes = recuperado.Value.ToList();
        foreach (var datos in datosPublicos) fuentes.AddRange(await datos.BuscarAsync(request.Pregunta, ct));
        var contexto = fundamento.Preparar(fuentes);
        if (contexto.Fuentes.Count == 0) return SinContexto(session, request.SessionId is null);
        var generado = await llm.ResponderAsync(new(request.Pregunta, contexto.Json, request.SessionId is null), ct);
        if (generado.IsFailure) return Result<ConsultaIaDto>.Failure(generado.Errors);
        var validado = fundamento.Validar(generado.Value.Texto, contexto, request.SessionId is null);
        if (validado.IsFailure) return SinContexto(session, request.SessionId is null, generado.Value);
        return Result<ConsultaIaDto>.Success(new(session, validado.Value.Respuesta, validado.Value.Fuentes, "OK", generado.Value.Modelo, generado.Value.TokensEntrada, generado.Value.TokensSalida));
    }
    private static Result<ConsultaIaDto> SinContexto(Guid session, bool presentarse, RespuestaLlm? respuesta = null) => Result<ConsultaIaDto>.Success(
        new(session, (presentarse ? "Hola, soy José. " : "") + "No tengo información pública suficiente para responder con fuentes verificables. Puedes indicar el evento o consultar a la organización.",
            [], "SIN_CONTEXTO", respuesta?.Modelo, respuesta?.TokensEntrada, respuesta?.TokensSalida));
}
