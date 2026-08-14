using Microsoft.EntityFrameworkCore;
using Sillar.Core;
using Sillar.Core.Authentication;
using Sillar.Core.Data;
using Sillar.Core.Modularity;
using Sillar.Shared.Modularity;

namespace Sillar.Api.Modularity;

/// <summary>Resultado del arranque modular.</summary>
/// <param name="Declared">Todos los módulos presentes en el despliegue.</param>
/// <param name="Active">Módulos activos, en orden de dependencia.</param>
/// <param name="IsSetupMode">La instalación está pendiente de completarse.</param>
internal sealed record ModuleBootstrapResult(
    IReadOnlyList<IModule> Declared,
    IReadOnlyList<IModule> Active,
    bool IsSetupMode);

/// <summary>
/// Ejecuta la secuencia de arranque del SPEC de CORE §7.
/// </summary>
/// <remarks>
/// El orden no es decorativo. Lo que se puede comprobar sin base de datos se
/// comprueba antes de tocarla, y los servicios se registran solo cuando ya se
/// sabe qué módulos están activos: por eso todo esto ocurre antes de construir
/// la aplicación, con un contexto de vida corta que se desecha al terminar.
/// </remarks>
internal static class ModuleBootstrapper
{
    /// <summary>Descubre, valida, sincroniza y registra los módulos activos.</summary>
    public static async Task<ModuleBootstrapResult> RunAsync(
        WebApplicationBuilder builder,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Sillar.Arranque");

        // --- Paso 1: descubrir los módulos --------------------------------
        var modules = ModuleDiscovery.Discover(logger);
        logger.LogInformation(
            "Módulos descubiertos: {Count} ({Codes}).",
            modules.Count,
            string.Join(", ", modules.Select(module => module.Code).Order()));

        // --- Paso 2: validar el grafo en memoria --------------------------
        // Un fallo aquí no depende de la instalación: es un error de cómo está
        // escrito el producto, así que se aborta sin llegar a la base de datos.
        var graph = ModuleGraph.Validate(modules);

        foreach (var warning in graph.Warnings)
        {
            logger.LogWarning("{Warning}", warning);
        }

        if (!graph.IsValid)
        {
            throw new StartupAbortedException(graph.DescribeErrors());
        }

        // --- Paso 3: conectar con la base de datos ------------------------
        var connectionString = builder.Configuration.GetConnectionString(CoreModule.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new StartupAbortedException(
                $"Falta la cadena de conexión '{CoreModule.ConnectionStringName}'. " +
                "Se define en el archivo .env de la raíz como ConnectionStrings__Default.");
        }

        await using var database = new CoreDbContext(CoreDataServiceExtensions.BuildOptions(connectionString));

        if (!await database.Database.CanConnectAsync(cancellationToken))
        {
            throw new StartupAbortedException(
                "No se pudo conectar con PostgreSQL. Comprueba que el contenedor está levantado " +
                "('docker compose up -d') y que ConnectionStrings__Default apunta al puerto correcto.");
        }

        await ApplyMigrationsIfAllowedAsync(builder, database, logger, cancellationToken);

        // --- Paso 3 bis: ¿modo instalación? -------------------------------
        var installationKey = await ReadInstallationKeyAsync(database, logger, cancellationToken);

        if (installationKey is null)
        {
            // Lo mínimo para que /api/setup* funcione: datos, reloj, hashes y
            // auditoría. Ni sesiones ni autorización: todavía no hay a quién
            // autenticar, ni installation_key de la que derivar la clave CSRF.
            builder.Services.AddCoreEssentials(builder.Configuration, connectionString);

            return new ModuleBootstrapResult(modules, [], IsSetupMode: true);
        }

        // La clave CSRF se deriva aquí y no antes: su origen es la fila de
        // core.installation que se acaba de leer (ADR-012). El host la deja en el
        // contenedor, igual que hace con la foto de activaciones.
        builder.Services.AddSingleton(new CsrfTokenFactory(installationKey.Value));

        // --- Pasos 4 y 5: sincronizar catálogo y activaciones -------------
        var synchronizer = new ModuleSynchronizer(database, loggerFactory.CreateLogger<ModuleSynchronizer>());
        var activeModules = await synchronizer.SynchronizeAsync(modules, cancellationToken);
        var activeCodes = activeModules.Select(module => module.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // --- Paso 6: las dependencias duras de lo activo, activas ---------
        var problems = ModuleGraph.ValidateActivations(modules, activeCodes);
        if (problems.Count > 0)
        {
            throw new StartupAbortedException(
                "Las activaciones de esta instalación son incoherentes:" + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(problem => "  · " + problem)));
        }

        // --- Paso 7: registrar solo lo activo -----------------------------
        // En orden de dependencia: un módulo puede necesitar en su registro
        // algo que dejó puesto aquel del que depende.
        var active = graph.InstallationOrder.Where(module => activeCodes.Contains(module.Code)).ToList();

        builder.Services.AddSingleton(new ModuleActivationSnapshot(activeModules));

        foreach (var module in active)
        {
            module.RegisterServices(builder.Services, builder.Configuration);
        }

        logger.LogInformation(
            "Módulos activos: {Codes}.",
            string.Join(", ", active.Select(module => module.Code)));

        var inactive = modules.Select(module => module.Code).Except(activeCodes).ToList();
        if (inactive.Count > 0)
        {
            logger.LogInformation(
                "Módulos instalados pero inactivos: {Codes}. Sus rutas no existen.",
                string.Join(", ", inactive));
        }

        return new ModuleBootstrapResult(modules, active, IsSetupMode: false);
    }

    /// <summary>
    /// Aplica las migraciones pendientes solo en desarrollo y solo si la
    /// configuración lo pide (ADR-009).
    /// </summary>
    /// <remarks>
    /// En producción no se aplican nunca al arrancar: con varias instalaciones
    /// en versiones distintas, una migración que se ejecuta sola es un incidente
    /// esperando ocurrir. Allí es un paso explícito del despliegue.
    /// </remarks>
    private static async Task ApplyMigrationsIfAllowedAsync(
        WebApplicationBuilder builder,
        CoreDbContext database,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var requested = builder.Configuration.GetValue<bool>("Sillar:Database:ApplyMigrationsOnStartup");

        if (requested && !builder.Environment.IsDevelopment())
        {
            logger.LogWarning(
                "Sillar:Database:ApplyMigrationsOnStartup está activada en el entorno {Environment}. " +
                "Se ignora: fuera de desarrollo las migraciones se aplican como paso explícito del despliegue.",
                builder.Environment.EnvironmentName);
            return;
        }

        if (!requested)
        {
            return;
        }

        var pending = (await database.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        logger.LogInformation("Aplicando {Count} migración(es) pendiente(s) de CORE: {Names}.",
            pending.Count, string.Join(", ", pending));
        await database.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Determina si la instalación sigue pendiente: sin schema, sin fila de
    /// instalación, o con la instalación sin completar.
    /// </summary>
    /// <returns>
    /// La clave de la instalación, o <c>null</c> si la instalación sigue
    /// pendiente y hay que arrancar en modo instalación.
    /// </returns>
    /// <remarks>
    /// Devuelve la clave y no solo un booleano porque de ella se deriva la clave
    /// CSRF (ADR-012). Es el único punto del arranque donde se lee
    /// <c>core.installation</c>, y ocurre en el paso 3: antes no existe la fila y
    /// no habría nada que derivar.
    /// </remarks>
    private static async Task<Guid?> ReadInstallationKeyAsync(
        CoreDbContext database,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var applied = (await database.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        if (applied.Count == 0)
        {
            LogSetupMode(
                logger,
                "el schema 'core' todavía no existe. Aplica las migraciones con " +
                "'dotnet ef database update --project Sillar.Core --startup-project Sillar.Api'");
            return null;
        }

        var pending = (await database.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogWarning(
                "Hay {Count} migración(es) de CORE sin aplicar ({Names}). El esquema de la base es más " +
                "antiguo que el código y pueden aparecer errores en tiempo de ejecución.",
                pending.Count,
                string.Join(", ", pending));
        }

        var installation = await database.Installations.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (installation is null)
        {
            LogSetupMode(logger, "no hay ninguna fila en core.installation");
            return null;
        }

        if (!installation.IsSetupComplete)
        {
            LogSetupMode(logger, "core.installation tiene is_setup_complete = false");
            return null;
        }

        return installation.InstallationKey;
    }

    private static void LogSetupMode(ILogger logger, string reason)
    {
        logger.LogWarning(
            "MODO INSTALACIÓN: {Reason}. No se registra ningún módulo y no se monta ninguna ruta de negocio. " +
            "Completa la instalación con POST /api/setup; el host se reiniciará en modo normal.",
            reason);
    }
}
