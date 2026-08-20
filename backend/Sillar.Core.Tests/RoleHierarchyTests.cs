using Sillar.Core.Authentication;
using Sillar.Core.Contracts;

namespace Sillar.Core.Tests;

/// <summary>Jerarquía de roles.</summary>
public class RoleHierarchyTests
{
    [Theory]
    [InlineData(AdminRole.SuperAdmin, AdminRole.Editor)]
    [InlineData(AdminRole.SuperAdmin, AdminRole.Admin)]
    [InlineData(AdminRole.SuperAdmin, AdminRole.SuperAdmin)]
    [InlineData(AdminRole.Admin, AdminRole.Editor)]
    [InlineData(AdminRole.Admin, AdminRole.Admin)]
    [InlineData(AdminRole.Editor, AdminRole.Editor)]
    public void Un_rol_satisface_lo_que_se_le_exige_por_debajo(string rol, string exigido)
    {
        Assert.True(RoleHierarchy.Satisfies(rol, exigido));
    }

    [Theory]
    [InlineData(AdminRole.Editor, AdminRole.Admin)]
    [InlineData(AdminRole.Editor, AdminRole.SuperAdmin)]
    [InlineData(AdminRole.Admin, AdminRole.SuperAdmin)]
    public void Un_rol_no_alcanza_lo_que_esta_por_encima(string rol, string exigido)
    {
        Assert.False(RoleHierarchy.Satisfies(rol, exigido));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("jefe")]
    [InlineData("Admin")]
    public void Un_rol_desconocido_no_satisface_nada(string? rol)
    {
        // 'Admin' con mayúscula tampoco: el valor viaja tal cual a la base, donde
        // un CHECK solo admite la forma en minúsculas.
        Assert.False(RoleHierarchy.Satisfies(rol, AdminRole.Editor));
    }
}
