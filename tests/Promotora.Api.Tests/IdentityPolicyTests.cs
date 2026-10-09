using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Promotora.WebApi.Identity;

namespace Promotora.Api.Tests;

public sealed class IdentityPolicyTests
{
    [Theory]
    [InlineData("CLIENTE", IdentityPolicies.Cliente, true)]
    [InlineData("CLIENTE", IdentityPolicies.Administrador, false)]
    [InlineData("CLIENTE", IdentityPolicies.DuenoRecinto, false)]
    [InlineData("ADMINISTRADOR", IdentityPolicies.Administrador, true)]
    [InlineData("ADMINISTRADOR", IdentityPolicies.Cliente, false)]
    [InlineData("DUENO_RECINTO", IdentityPolicies.DuenoRecinto, true)]
    [InlineData("DUENO_RECINTO", IdentityPolicies.Administrador, false)]
    public async Task PolicyExigeElRolCorrectoYRechazaPasswordPendiente(string rol, string policy, bool allowed)
    {
        await using var factory = new ApiFactory();
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = Principal(rol, false);
        Assert.Equal(allowed, (await authorization.AuthorizeAsync(principal, null, policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(Principal(rol, true), null, policy)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(Principal(rol, true), null, IdentityPolicies.CambioPassword)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(), null, policy)).Succeeded);
    }

    private static ClaimsPrincipal Principal(string role, bool change) => new(new ClaimsIdentity(
        [new Claim("sub", "1"), new Claim("role", role), new Claim("password_change_required", change ? "true" : "false")], "test", "sub", "role"));
}
