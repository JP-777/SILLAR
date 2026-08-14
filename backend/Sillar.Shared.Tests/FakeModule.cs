using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Shared.Modularity;

namespace Sillar.Shared.Tests;

/// <summary>
/// Módulo de mentira para probar el validador del grafo.
/// </summary>
/// <remarks>
/// Existe para poder describir instalaciones imposibles —un ciclo, una
/// dependencia hacia un módulo que no existe— sin meter módulos falsos en el
/// producto. No registra servicios ni monta rutas: al validador no le importa.
/// </remarks>
internal sealed class FakeModule(
    string code,
    string[]? hard = null,
    string[]? soft = null,
    int displayOrder = 1,
    string? displayName = null,
    string? version = null,
    string? description = null) : IModule
{
    public string Code => code;

    public string DisplayName => displayName ?? $"Módulo {code}";

    public string Description => description ?? $"Lo que hace el módulo {code}.";

    public string Version => version ?? "1.0.0";

    public int DisplayOrder => displayOrder;

    public string[] HardDependencies => hard ?? [];

    public string[] SoftDependencies => soft ?? [];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

/// <summary>Atajos para armar instalaciones de prueba.</summary>
internal static class Instalacion
{
    /// <summary>CORE, que no depende de nadie y va siempre el primero.</summary>
    public static IModule Core() => new FakeModule("core", displayOrder: 0);

    /// <summary>Un módulo cualquiera, que como todos depende de CORE.</summary>
    public static IModule Modulo(string code, string[]? duras = null, string[]? blandas = null, int orden = 1)
        => new FakeModule(code, hard: ["core", .. duras ?? []], soft: blandas, displayOrder: orden);
}
