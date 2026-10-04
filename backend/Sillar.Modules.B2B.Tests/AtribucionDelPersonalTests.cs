using System.Text.RegularExpressions;
using Sillar.Core.Contracts;
using Sillar.Modules.B2B.Bandeja;
using Sillar.Modules.B2B.Domain;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// La atribución del personal en M07: tres datos congelados que forman una unidad
/// (R-14).
/// </summary>
/// <remarks>
/// <para>
/// Las de arriba van contra <see cref="AtribucionDelPersonal"/> y no tocan la base:
/// que la atribución esté completa o no esté es una decisión, no una consulta, y
/// probarla en milisegundos es lo que hace que alguien la provoque. El ciclo de pago
/// contra PostgreSQL vive en <c>CotizacionesTests</c>, donde el <c>CHECK</c> puede
/// decir no.
/// </para>
/// <para>
/// Las de abajo son afirmaciones sobre el <b>esquema</b>, y se comprueban leyendo la
/// migración, que es la fuente de verdad del esquema (ADR-009). No se usa metadata de
/// EF: las claves foráneas cruzadas no están en el modelo —no pueden estarlo— así que
/// preguntarle a EF daría un falso verde.
/// </para>
/// <para>
/// <b>Es la hermana de <c>Sillar.Modules.Sales.Tests.AtribucionDelPersonalTests</c></b>
/// y está escrita a propósito dos veces: un módulo no importa de otro, y M07 tenía la
/// regla escrita en cuatro comentarios sin nada que la comprobara.
/// </para>
/// </remarks>
public sealed class AtribucionDelPersonalTests
{
    private static string Migracion()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);

        while (raiz is not null && !Directory.Exists(Path.Combine(raiz.FullName, "Sillar.Modules.B2B")))
        {
            raiz = raiz.Parent;
        }

        Assert.NotNull(raiz);

        var carpeta = Path.Combine(raiz!.FullName, "Sillar.Modules.B2B", "Migrations");
        var archivo = Directory.GetFiles(carpeta, "*_B2bInitial.cs").Single();
        return File.ReadAllText(archivo);
    }

    /// <summary>Una sesión completa, con los tres datos distintos entre sí.</summary>
    private sealed class Sesion : ICurrentAdmin
    {
        public int? AdminUserId { get; init; } = 7;
        public string? Email { get; init; } = "ana@ejemplo.test";
        public string? DisplayName { get; init; } = "Ana Quispe";
        public string? HomeNode { get; init; } = "principal";
        public string? Role => "admin";
        public bool IsInRole(string role) => role is "admin" or "editor";
    }

    // ================================================================
    // 1. Los tres datos, y de dónde sale cada uno.
    // ================================================================

    [Fact]
    public void Una_sesion_completa_da_los_tres_datos()
    {
        var atribucion = AtribucionDelPersonal.De(new Sesion());

        Assert.NotNull(atribucion);
        Assert.Equal(("Ana Quispe", 7, "principal"), (atribucion!.Name, atribucion.LocalId, atribucion.HomeNode));
    }

    [Fact]
    public void El_nombre_visible_nunca_es_el_correo()
    {
        // El doble da correo y nombre distintos a propósito: si alguien volviera a
        // escribir «u.Email ?? …» donde va el nombre, se vería aquí y no en una
        // bitácora dentro de un año.
        var atribucion = AtribucionDelPersonal.De(new Sesion { DisplayName = "Ana Quispe", Email = "ana@ejemplo.test" });

        Assert.Equal("Ana Quispe", atribucion!.Name);
        Assert.DoesNotContain("@", atribucion.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void El_nodo_es_el_de_la_cuenta_y_no_se_deriva_del_identificador()
    {
        // Son hechos distintos: el identificador local vive dentro del nodo, pero el
        // nodo no se lee del identificador. El doble los da sin relación ninguna.
        var atribucion = AtribucionDelPersonal.De(new Sesion { AdminUserId = 7, HomeNode = "sucursal-cayma" });

        Assert.Equal("sucursal-cayma", atribucion!.HomeNode);
    }

    // ================================================================
    // 2. Falta uno de los tres: no hay atribución, y no se inventa.
    // ================================================================

    [Fact]
    public void Sin_nombre_visible_no_hay_atribucion()
    {
        foreach (var vacio in new string?[] { null, "", "   " })
        {
            Assert.Null(AtribucionDelPersonal.De(new Sesion { DisplayName = vacio }));
        }
    }

    [Fact]
    public void Sin_nodo_de_la_cuenta_no_hay_atribucion()
    {
        foreach (var vacio in new string?[] { null, "", "   " })
        {
            Assert.Null(AtribucionDelPersonal.De(new Sesion { HomeNode = vacio }));
        }
    }

    [Fact]
    public void Sin_identificador_local_valido_no_hay_atribucion()
    {
        // El cero entra en la lista porque es el valor al que llega quien escribe
        // «AdminUserId ?? 0» para quitarse el nullable de encima: un identificador
        // que no es de nadie pasaría por uno real.
        foreach (var invalido in new int?[] { null, 0, -1 })
        {
            Assert.Null(AtribucionDelPersonal.De(new Sesion { AdminUserId = invalido }));
        }
    }

    [Fact]
    public void La_atribucion_llega_recortada()
    {
        var atribucion = AtribucionDelPersonal.De(new Sesion { DisplayName = "  Ana Quispe  ", HomeNode = " principal " });

        Assert.Equal(("Ana Quispe", "principal"), (atribucion!.Name, atribucion.HomeNode));
    }

    // ================================================================
    // 3. Cero FK hacia core.admin_users.
    // ================================================================

    [Fact]
    public void La_migracion_no_declara_ninguna_clave_foranea_hacia_core_admin_users()
    {
        // Se busca la DECLARACIÓN, no la palabra: la migración y la configuración
        // explican por escrito que NO hay clave foránea, y un barrido que prohibiera
        // «admin_users» a secas pararía en falso sobre su propia documentación. Una
        // barrera que para en falso te para; es la misma enfermedad que una que calla.
        var migracion = Migracion();

        Assert.DoesNotMatch(new Regex("REFERENCES\\s+core\\.admin_users", RegexOptions.IgnoreCase), migracion);
        Assert.DoesNotMatch(new Regex("principalTable:\\s*\"admin_users\"", RegexOptions.IgnoreCase), migracion);
        Assert.DoesNotMatch(new Regex("FOREIGN\\s+KEY[^;]*admin_users", RegexOptions.IgnoreCase), migracion);
    }

    [Fact]
    public void La_migracion_solo_referencia_los_dos_schemas_de_sus_dependencias_duras()
    {
        // M07 depende duro de M01 y M04, y de nadie más. Que «core» no aparezca es
        // lo que impide que la atribución se convierta en un puntero por descuido.
        var referencias = Regex.Matches(Migracion(), @"REFERENCES\s+(\w+)\.")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["catalog", "crm"], referencias);
    }

    // ================================================================
    // 4. La atribución vive en la actuación, no en la cabecera.
    // ================================================================

    [Fact]
    public void La_cotizacion_solo_atribuye_el_pago_y_ninguna_otra_actuacion()
    {
        // Enviar y aprobar no congelan a nadie: la aprobación es del cliente, y el
        // envío no es el hecho que haga falta reconstruir dentro de un año. Si
        // mañana hiciera falta, es otra tabla de actuaciones —como en M03—, no tres
        // columnas más en la cabecera.
        var atribuciones = typeof(Quote).GetProperties()
            .Select(p => p.Name)
            .Where(n => n.Contains("RegisteredBy", StringComparison.Ordinal)
                     || n.Contains("AdminUser", StringComparison.Ordinal)
                     || n.Contains("ApprovedBy", StringComparison.Ordinal)
                     || n.Contains("SentBy", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "PaidRegisteredBy",
                "PaidRegisteredByAdminUserHomeNode",
                "PaidRegisteredByAdminUserLocalId",
            ],
            atribuciones);
    }
}
