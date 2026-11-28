using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
namespace Sillar.Core.Tests;
/// <summary>Autoridad explícita en pruebas de esquema; la sesión real se prueba en LicenciasYPermisosPersistenciaTests.</summary>
internal sealed class AutoridadDeActivacionFalsa : IAdminSessionAuthority
{
    public Task<AuthorizedAdmin> RequireAsync(string role, CancellationToken ct)
        => Task.FromResult(new AuthorizedAdmin(1, "Operador de prueba", "principal", "operador@ejemplo.test", AdminRole.SuperAdmin));
}
