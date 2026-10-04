using System.Text.RegularExpressions;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// La atribución del personal: tres datos congelados que forman una unidad.
/// </summary>
/// <remarks>
/// <para>
/// Las siete pruebas que el encargo exige. Las cuatro primeras van contra el tipo
/// puro <see cref="StaffAttribution"/> y no tocan la base: la coherencia de la
/// atribución es una decisión, no una consulta, y probarla en milisegundos es lo
/// que hace que alguien la provoque.
/// </para>
/// <para>
/// Las tres últimas son afirmaciones sobre el <b>esquema</b>, y se comprueban
/// leyendo la migración, que es la fuente de verdad del esquema (ADR-009). No se
/// usa metadata de EF: las dos claves foráneas cruzadas no están en el modelo —no
/// pueden estarlo— así que preguntarle a EF daría un falso verde.
/// </para>
/// </remarks>
public sealed class AtribucionDelPersonalTests
{
    private static string Migracion()
    {
        var aqui = AppContext.BaseDirectory;
        var raiz = new DirectoryInfo(aqui);

        while (raiz is not null && !Directory.Exists(Path.Combine(raiz.FullName, "Sillar.Modules.Sales")))
        {
            raiz = raiz.Parent;
        }

        Assert.NotNull(raiz);

        var carpeta = Path.Combine(raiz!.FullName, "Sillar.Modules.Sales", "Migrations");
        var archivo = Directory.GetFiles(carpeta, "*_SalesInitial.cs").Single();
        return File.ReadAllText(archivo);
    }

    // ================================================================
    // 1. Pago humano: los tres datos obligatorios.
    // ================================================================

    [Fact]
    public void Un_pago_humano_exige_los_tres_datos()
    {
        var completa = StaffAttribution.De("Ana Quispe", 7, "principal");

        Assert.Equal("Ana Quispe", completa.Name);
        Assert.Equal(7, completa.LocalId);
        Assert.Equal("principal", completa.HomeNode);
    }

    [Fact]
    public void Un_pago_sin_nombre_no_se_registra()
    {
        foreach (var vacio in new string?[] { null, "", "   " })
        {
            Assert.Throws<ArgumentException>(() => StaffAttribution.De(vacio, 7, "principal"));
        }
    }

    [Fact]
    public void Un_pago_sin_identificador_local_no_se_registra()
    {
        Assert.Throws<ArgumentException>(() => StaffAttribution.De("Ana Quispe", null, "principal"));
    }

    [Fact]
    public void Un_identificador_local_cero_o_negativo_no_se_acepta()
    {
        // Cero sería un trabajador ficticio con apariencia de real.
        foreach (var invalido in new[] { 0, -1 })
        {
            Assert.Throws<ArgumentException>(() => StaffAttribution.De("Ana Quispe", invalido, "principal"));
        }
    }

    [Fact]
    public void Un_pago_sin_nodo_de_la_cuenta_no_se_registra()
    {
        // Sin él, el identificador local es un entero sin universo.
        foreach (var vacio in new string?[] { null, "", "  " })
        {
            Assert.Throws<ArgumentException>(() => StaffAttribution.De("Ana Quispe", 7, vacio));
        }
    }

    // ================================================================
    // 2 y 3. Cambio humano: los tres presentes. Cambio automático: los tres NULL.
    // ================================================================

    [Fact]
    public void Un_cambio_humano_lleva_los_tres_datos_presentes()
    {
        var atribucion = StaffAttribution.DeOpcional("Ana Quispe", 7, "principal");

        Assert.NotNull(atribucion);
        Assert.Equal(7, atribucion!.LocalId);
    }

    [Fact]
    public void Un_cambio_automatico_lleva_los_tres_datos_nulos_y_no_atribuye_a_nadie()
    {
        // El vencimiento del plazo de pago lo provoca el tiempo, no una persona.
        Assert.Null(StaffAttribution.DeOpcional(null, null, null));
    }

    [Fact]
    public void Un_cambio_automatico_leido_desde_la_entidad_no_atribuye_a_nadie()
    {
        var vencimiento = new OrderStatusChange { ToStatus = "expired" };

        Assert.Null(vencimiento.Atribucion());
    }

    [Fact]
    public void Ninguna_de_las_seis_atribuciones_parciales_se_acepta()
    {
        // Seis combinaciones incompletas: ni son actuación del sistema ni humana.
        // Media atribución es peor que ninguna, porque parece completa.
        (string? N, int? I, string? H)[] parciales =
        [
            ("Ana", null, null),
            (null, 7, null),
            (null, null, "principal"),
            ("Ana", 7, null),
            ("Ana", null, "principal"),
            (null, 7, "principal")
        ];

        foreach (var (n, i, h) in parciales)
        {
            Assert.Throws<ArgumentException>(() => StaffAttribution.DeOpcional(n, i, h));
        }
    }

    // ================================================================
    // 4. account-node != action origin_node es válido.
    // ================================================================

    [Fact]
    public void El_nodo_de_la_cuenta_puede_ser_distinto_del_nodo_de_la_actuacion()
    {
        // Una cuenta del nodo A registra una actuación desde el nodo B. Es un hecho
        // real del negocio y la atribución lo admite sin queja.
        var pago = new OrderPayment
        {
            Method = "yape",
            RegisteredBy = "Ana Quispe",
            RegisteredByAdminUserLocalId = 7,
            RegisteredByAdminUserHomeNode = "A",   // la cuenta vive en A
            OriginNode = "B"                        // la actuación ocurrió en B
        };

        Assert.NotEqual(pago.RegisteredByAdminUserHomeNode, pago.OriginNode);
        Assert.Equal("A", pago.RegisteredByAdminUserHomeNode);
        Assert.Equal("B", pago.OriginNode);
    }

    [Fact]
    public void Ningun_CHECK_exige_que_los_dos_nodos_coincidan()
    {
        // Exigirlo prohibiría un hecho real. Se comprueba sobre la migración, que es
        // la fuente de verdad del esquema: ningún CHECK compara las dos columnas.
        var migracion = Migracion();

        Assert.DoesNotContain("registered_by_admin_user_home_node = origin_node", migracion);
        Assert.DoesNotContain("origin_node = registered_by_admin_user_home_node", migracion);
        Assert.DoesNotContain("changed_by_admin_user_home_node = origin_node", migracion);
        Assert.DoesNotContain("origin_node = changed_by_admin_user_home_node", migracion);
    }

    [Fact]
    public void El_nodo_de_la_cuenta_no_se_deriva_del_nodo_de_la_actuacion()
    {
        // La atribución se construye con su nodo explícito y el tipo NO conoce el
        // nodo de la instalación: si pudiera alcanzarlo, alguien derivaría uno del
        // otro y funcionaría hasta el día que alguien atienda desde otra sucursal.
        var propiedades = typeof(StaffAttribution).GetProperties().Select(p => p.Name).ToArray();

        Assert.Contains("HomeNode", propiedades);
        Assert.DoesNotContain("OriginNode", propiedades);
    }

    // ================================================================
    // 5. El origin_node del pedido no interviene en la interpretación.
    // ================================================================

    [Fact]
    public void El_pedido_no_interviene_en_la_interpretacion_de_la_atribucion()
    {
        // La atribución es autosuficiente: lleva sus tres datos y no alcanza al
        // pedido. Ni una referencia, ni un identificador de pedido, ni su nodo.
        var propiedades = typeof(StaffAttribution).GetProperties().Select(p => p.Name).ToArray();

        Assert.Equal(["Name", "LocalId", "HomeNode"], propiedades);
    }

    [Fact]
    public void La_atribucion_de_un_cambio_se_resuelve_sin_leer_el_pedido()
    {
        // Se construye un asiento con su pedido apuntando a cualquier sitio: la
        // atribución sale igual, porque no lo mira.
        var asiento = new OrderStatusChange
        {
            OrderId = Guid.CreateVersion7(),
            ToStatus = "preparing",
            ChangedBy = "Ana Quispe",
            ChangedByAdminUserLocalId = 7,
            ChangedByAdminUserHomeNode = "A",
            OriginNode = "B"
        };

        var atribucion = asiento.Atribucion();

        Assert.NotNull(atribucion);
        Assert.Equal("A", atribucion!.HomeNode);
    }

    // ================================================================
    // 6. Cero FK hacia core.admin_users.
    // ================================================================

    [Fact]
    public void La_migracion_no_declara_ninguna_clave_foranea_hacia_core_admin_users()
    {
        // Se busca la DECLARACION, no la palabra. La primera version de esta prueba
        // buscaba «admin_users» a secas y paro en falso sobre el comentario de
        // columna que explica que NO hay clave foranea — el mismo defecto que tuvo
        // el barrido de R-13, y por el mismo motivo: prohibir la palabra en vez del
        // hecho. Una barrera que para en falso te para; es la misma enfermedad que
        // una que calla.
        var migracion = Migracion();

        Assert.DoesNotMatch(new Regex("REFERENCES\\s+core\\.admin_users", RegexOptions.IgnoreCase), migracion);
        Assert.DoesNotMatch(new Regex("principalTable:\\s*\"admin_users\"", RegexOptions.IgnoreCase), migracion);
        Assert.DoesNotMatch(new Regex("FOREIGN\\s+KEY[^;]*admin_users", RegexOptions.IgnoreCase), migracion);
    }

    [Fact]
    public void La_migracion_solo_referencia_dos_schemas_ajenos_y_ninguno_es_core()
    {
        var migracion = Migracion();
        var referencias = Regex.Matches(migracion, @"REFERENCES\s+(\w+)\.")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["catalog", "crm"], referencias);
    }

    // ================================================================
    // 7. Cero columnas de atribución en sales.orders.
    // ================================================================

    [Fact]
    public void La_cabecera_del_pedido_no_atribuye_a_ningun_trabajador()
    {
        // Si intervienen varias personas, un único responsable histórico sería
        // ambiguo. La atribución vive solo en el registro de cada actuación.
        var propiedades = typeof(Order).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(propiedades, p =>
            p.Contains("AdminUser", StringComparison.Ordinal) ||
            p.Contains("RegisteredBy", StringComparison.Ordinal) ||
            p.Contains("ChangedBy", StringComparison.Ordinal) ||
            p.Contains("AttendedBy", StringComparison.Ordinal) ||
            p.Contains("Atendido", StringComparison.Ordinal));
    }

    [Fact]
    public void La_tabla_orders_de_la_migracion_no_tiene_columna_de_atribucion()
    {
        var migracion = Migracion();
        var inicio = migracion.IndexOf("name: \"orders\"", StringComparison.Ordinal);
        var fin = migracion.IndexOf("name: \"order_lines\"", StringComparison.Ordinal);

        Assert.True(inicio > 0 && fin > inicio);
        var bloque = migracion[inicio..fin];

        foreach (var prohibida in new[] { "admin_user", "registered_by", "changed_by", "attended_by", "atendido" })
        {
            Assert.DoesNotContain(prohibida, bloque, StringComparison.OrdinalIgnoreCase);
        }
    }
}
