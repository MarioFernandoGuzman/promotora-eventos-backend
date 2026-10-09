namespace Promotora.WebApi.Identity;

public sealed class LowercaseRouteTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value) => value?.ToString()?.ToLowerInvariant();
}
