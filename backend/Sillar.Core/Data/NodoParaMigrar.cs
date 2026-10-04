using Microsoft.Extensions.Configuration;
using Npgsql;
using Sillar.Shared.Replication;

namespace Sillar.Core.Data;

/// <summary>
/// Cómo llega a una migración de CORE el nodo configurado de esta instalación.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué hace falta.</b> La migración que rellena <c>core.admin_users.home_node</c>
/// sobre cuentas ya existentes tiene que saber en qué nodo viven. Una migración
/// de EF no recibe servicios ni configuración —la crea EF, no el contenedor— y
/// el contrato <c>IModuleMigrations</c> solo pasa una cadena de conexión. Así
/// que el dato viaja <b>en la conexión</b>, como parámetro de sesión de
/// PostgreSQL (<c>-c sillar.node_code=…</c> en <c>Options</c>), y la migración lo
/// lee con <c>current_setting('sillar.node_code', true)</c>.
/// </para>
/// <para>
/// <b>Solo el valor configurado, nunca uno por defecto.</b> Si
/// <c>Sillar:Node:Code</c> no está configurado, la conexión no lleva nada y la
/// migración <b>aborta</b> en cuanto haya una cuenta que rellenar. Un relleno
/// de datos existentes no adivina de dónde son: es la misma razón por la que
/// <see cref="NodeIdentity"/> no pone el nodo como DEFAULT de la base.
/// </para>
/// <para>
/// Lo ponen los tres caminos que aplican migraciones de CORE: el instalador
/// (<c>POST /api/setup</c>), la migración al arrancar en desarrollo y la fábrica
/// de diseño de <c>dotnet ef</c>. Ninguno toca el contrato compartido.
/// </para>
/// </remarks>
public static class NodoParaMigrar
{
    /// <summary>Parámetro de sesión que lee la migración.</summary>
    public const string Parametro = "sillar.node_code";

    /// <summary>La misma clave de configuración, escrita como variable de entorno.</summary>
    public static readonly string VariableDeEntorno = NodeIdentity.SettingKey.Replace(":", "__", StringComparison.Ordinal);

    /// <summary>El nodo configurado, o <c>null</c> si no hay ninguno. Sin valor por defecto.</summary>
    public static string? Configurado(IConfiguration configuration)
        => Limpio(configuration[NodeIdentity.SettingKey]);

    /// <summary>El nodo configurado en el entorno del proceso, o <c>null</c>.</summary>
    /// <remarks>Para la fábrica de diseño, que no tiene <c>IConfiguration</c>.</remarks>
    public static string? DelEntorno()
        => Limpio(Environment.GetEnvironmentVariable(VariableDeEntorno));

    /// <summary>
    /// La cadena con el nodo como parámetro de sesión. Sin nodo, la cadena tal cual.
    /// </summary>
    /// <remarks>
    /// Conserva lo que ya hubiera en <c>Options</c>. El valor se escapa como pide
    /// PostgreSQL para las opciones de arranque: barra invertida y espacio.
    /// </remarks>
    public static string ConNodo(string connectionString, string? nodo)
    {
        if (nodo is null)
        {
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var escapado = nodo.Replace(@"\", @"\\", StringComparison.Ordinal).Replace(" ", @"\ ", StringComparison.Ordinal);
        var opcion = $"-c {Parametro}={escapado}";

        builder.Options = string.IsNullOrWhiteSpace(builder.Options) ? opcion : $"{builder.Options} {opcion}";
        return builder.ConnectionString;
    }

    private static string? Limpio(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
