namespace Promotora.Application.Shared;

public sealed record PaginaDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
