namespace Sillar.Shared.Data.Modularity;

/// <summary>
/// Capacidad opcional de un módulo con persistencia: aplicar <b>sus propias</b>
/// migraciones y decir qué sabe de ellas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué es opcional y por qué vive aquí.</b> No está en <c>IModule</c>
/// porque <c>Sillar.Shared</c> declara que en él no entra acceso a datos, y una
/// migración lo es. Vive en <c>Sillar.Shared.Data</c>, el proyecto de
/// persistencia de la plataforma, que CORE y los módulos ya referencian. Un
/// módulo sin persistencia —los de demostración— sencillamente no la
/// implementa.
/// </para>
/// <para>
/// <b>Cada módulo aplica las suyas.</b> Quien implementa esto construye su
/// propio <c>DbContext</c>, con la misma tabla de historial que usa en tiempo
/// de ejecución. El instalador solo descubre y orquesta: recorre los módulos
/// declarados y pregunta cuáles implementan esta interfaz. Así CORE no
/// referencia a ningún módulo ni se apropia de sus migraciones — ADR-009: la
/// autoridad del esquema de cada uno siguen siendo sus migraciones.
/// </para>
/// <para>
/// El schema al que pertenecen no se repite aquí: es <c>IModule.Schema</c> del
/// mismo objeto.
/// </para>
/// </remarks>
public interface IModuleMigrations
{
    /// <summary>Tabla de historial de EF, dentro del schema del módulo.</summary>
    string MigrationsHistoryTable { get; }

    /// <summary>
    /// Todas las migraciones que <b>este binario</b> conoce para el módulo.
    /// </summary>
    /// <remarks>
    /// Sirve para reconocer un historial como propio: si la base tiene
    /// aplicadas migraciones que este binario no trae, no es una instalación a
    /// medias de esta versión, y el instalador no debe tocarla.
    /// </remarks>
    IReadOnlyList<string> KnownMigrations(string connectionString);

    /// <summary>
    /// Las migraciones del módulo que ya están aplicadas en esa base. Vacío si
    /// no hay historial.
    /// </summary>
    Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString, CancellationToken cancellationToken);

    /// <summary>Aplica las que falten. Idempotente: las aplicadas no se repiten.</summary>
    Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken);
}
