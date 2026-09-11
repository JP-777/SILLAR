using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Modularity;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;

namespace Sillar.Core.Setup;

/// <summary>Lo que hizo la preparación de la base.</summary>
/// <param name="Diagnostico">Qué era el destino, y por qué.</param>
/// <param name="Migrados">Códigos de los módulos cuyas migraciones se aplicaron, en orden.</param>
internal sealed record PreparacionDeLaBase(DiagnosticoDelDestino Diagnostico, IReadOnlyList<string> Migrados);

/// <summary>
/// Deja preparados los schemas de todos los módulos desplegados, como parte de
/// una instalación deliberada.
/// </summary>
/// <remarks>
/// <para>
/// <b>El instalador aplica; la activación comprueba.</b> Esta clase es la mitad
/// que aplica, y solo se usa desde <c>POST /api/setup</c>. El interruptor del
/// panel nunca migra: ver <c>ModuleActivationService</c>.
/// </para>
/// <para>
/// <b>Solo descubre y orquesta.</b> Recorre los módulos declarados y pregunta
/// cuáles implementan <see cref="IModuleMigrations"/>; cada uno aplica las
/// suyas con su propio <c>DbContext</c>. CORE no referencia a ningún módulo.
/// </para>
/// <para>
/// <b>No hay atomicidad entre módulos, y no se finge.</b> Las migraciones de
/// contextos distintos no forman una transacción. Lo que se garantiza es que se
/// puede <b>reanudar</b>: las aplicadas no se repiten, y una base a medias que
/// solo contiene cosas de SILLAR vuelve a pasar la comprobación del destino.
/// </para>
/// </remarks>
internal sealed class InstaladorDeModulos(DeclaredModules declared, ILogger<InstaladorDeModulos>? logger = null)
{
    private readonly ILogger log = logger ?? NullLogger<InstaladorDeModulos>.Instance;

    /// <summary>
    /// Los módulos con migraciones, en el orden en que se aplican.
    /// </summary>
    /// <remarks>
    /// <b>CORE va primero y se pone primero a propósito</b>, no porque el grafo
    /// lo deje ahí: los demás usan sus colaciones (<c>core.es_ci</c>) y referencian
    /// <c>core.media_assets</c>. El resto sigue el orden de instalación del grafo,
    /// que es determinista —dependencias primero, y a igualdad, por
    /// <c>DisplayOrder</c> y código—, nunca el de la reflexión o el contenedor.
    /// </remarks>
    public IReadOnlyList<IModule> Orden()
    {
        var grafo = ModuleGraph.Validate(declared.Modules);

        if (!grafo.IsValid)
        {
            throw new InvalidOperationException(
                "El grafo de módulos no es válido, y no se instala sobre un grafo roto: " + grafo.DescribeErrors());
        }

        var conMigraciones = grafo.InstallationOrder.Where(modulo => modulo is IModuleMigrations).ToList();

        foreach (var modulo in conMigraciones.Where(modulo => modulo.Schema is null))
        {
            throw new InvalidOperationException(
                $"El módulo '{modulo.Code}' tiene migraciones pero declara que no tiene schema. " +
                "Es un error de cómo está escrito el módulo: una de las dos cosas sobra.");
        }

        var core = conMigraciones.FirstOrDefault(modulo => modulo.Code == ModuleGraph.CoreCode)
            ?? throw new InvalidOperationException(
                "CORE no está entre los módulos con migraciones: sin él no hay nada sobre lo que instalar.");

        return [core, .. conMigraciones.Where(modulo => modulo.Code != ModuleGraph.CoreCode)];
    }

    /// <summary>Los schemas que son de SILLAR en esta instalación.</summary>
    public IReadOnlySet<string> SchemasDeSillar()
        => Orden().Select(modulo => modulo.Schema!).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Comprueba el destino y, solo si es seguro, aplica las migraciones de
    /// todos los módulos desplegados.
    /// </summary>
    /// <param name="connectionString">
    /// La conexión <b>real</b> —la del contexto de CORE que atiende la
    /// petición—, no la de la configuración: es la que se va a modificar.
    /// </param>
    /// <param name="cancellationToken">Cancelación.</param>
    public async Task<PreparacionDeLaBase> PrepararAsync(string connectionString, CancellationToken cancellationToken)
    {
        var orden = Orden();
        var modulos = orden.Select(modulo => (modulo.Schema!, (IModuleMigrations)modulo)).ToList();

        // Antes de tocar nada. Si el destino no es seguro, no se aplica ni una
        // migración: no hay «deshacer» para un schema creado en la base de otro.
        var diagnostico = await DestinoDeInstalacion.InspeccionarAsync(connectionString, modulos, cancellationToken);

        if (diagnostico.Estado == EstadoDelDestino.NoSeguro)
        {
            log.LogWarning(
                "Instalación rechazada en la base {Destino}: {Evidencias}.",
                diagnostico.Destino,
                string.Join("; ", diagnostico.Evidencias));

            return new PreparacionDeLaBase(diagnostico, []);
        }

        var migrados = new List<string>();

        foreach (var (modulo, (_, migraciones)) in orden.Zip(modulos))
        {
            log.LogInformation("Instalación: aplicando las migraciones de '{Modulo}'.", modulo.Code);
            await migraciones.ApplyMigrationsAsync(connectionString, cancellationToken);
            migrados.Add(modulo.Code);
        }

        return new PreparacionDeLaBase(diagnostico, migrados);
    }
}
