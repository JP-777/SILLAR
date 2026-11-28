using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
namespace Sillar.Modules.Reports.Tests;

/// <summary>Precondición explícita de los escenarios que prueban datos, zona o auditoría.</summary>
internal sealed class AutoridadDePrueba : IModuleAuthorization
{
    public int? Rechazo { get; set; }
    public List<string> Consultas { get; } = [];
    public Task<AuthorizedAdmin> RequireAsync(string moduleCode, string operationCode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Consultas.Add($"{moduleCode}:{operationCode}");
        if (Rechazo is { } status) throw new ModuleAccessDeniedException(status, "Rechazo deliberado del permiso.");
        return Task.FromResult(new AuthorizedAdmin(7, "Operadora", "principal", "operadora@ejemplo.test", AdminRole.Admin));
    }
}
internal static class PermisosDePrueba
{
    public static IServiceCollection ConPermisoDePrueba(this IServiceCollection services)
        => services.AddSingleton<IModuleAuthorization, AutoridadDePrueba>();
}
