using Promotora.Domain.Identity;

namespace Promotora.Domain.Tests;

public sealed class UsuarioAuthorizationTests
{
    [Theory]
    [InlineData(RolUsuario.CLIENTE, "ACTIVO", false, true, false)]
    [InlineData(RolUsuario.DUENO_RECINTO, "ACTIVO", false, true, false)]
    [InlineData(RolUsuario.ADMINISTRADOR, "ACTIVO", false, true, true)]
    [InlineData(RolUsuario.CLIENTE, "INACTIVO", false, false, false)]
    [InlineData(RolUsuario.ADMINISTRADOR, "SUSPENDIDO", false, false, false)]
    [InlineData(RolUsuario.ADMINISTRADOR, "ACTIVO", true, false, false)]
    public void PropiedadRolEstadoYCambioObligatorioGobiernanGestion(RolUsuario rol, string estado, bool mustChange, bool own, bool other)
    {
        var usuario = new Usuario(10, "test@example.test", "hash-no-utilizado", "Nombre", "Apellido", rol, estado, mustChange, DateTime.UnixEpoch);
        Assert.Equal(own, usuario.PuedeGestionarUsuario(10));
        Assert.Equal(other, usuario.PuedeGestionarUsuario(20));
        Assert.Equal(other, usuario.EsAdministradorHabilitado);
        Assert.False(usuario.PuedeCambiarPassword(20));
        Assert.Equal(estado == "ACTIVO", usuario.PuedeCambiarPassword(10));
    }
}
