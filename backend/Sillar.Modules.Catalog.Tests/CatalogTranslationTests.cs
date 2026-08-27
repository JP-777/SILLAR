using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Catalog.Services;
using Sillar.Shared.Configuration;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Catalog.Tests;

/// <summary>
/// Que las consultas del contrato de selección <b>se traducen a SQL y se
/// ejecutan</b>, contra una base de verdad.
/// </summary>
/// <remarks>
/// <para>
/// Existe por un fallo concreto: <c>GET /api/admin/catalog/brands</c> devolvió
/// 500 durante días con toda su lógica en verde, porque
/// <c>.Select(instancia.Metodo)</c> no lo traduce EF Core. <b>Nada que solo se
/// rompa cuando EF traduce a SQL es visible para una prueba en memoria</b>, y
/// la regla de la casa —«las pruebas de lógica no tocan la base»— tiene ahí su
/// punto ciego.
/// </para>
/// <para>
/// El contrato no tiene endpoint, así que <c>api-traduccion.spec.ts</c> no
/// puede cubrirlo. Y esperar a que M02 lo consuma dejaría la cobertura
/// colgando de otro equipo y otra fecha, mientras cualquiera puede tocar la
/// composición que ahora comparten buscar y releer.
/// </para>
/// <para>
/// <b>Se salta si no hay base</b>, en vez de fallar: la suite individual tiene
/// que poder correr en una máquina sin Docker. La puerta canónica, en cambio,
/// exige cero omisiones y siempre le proporciona una base PostgreSQL efímera.
/// Las pruebas que necesitan un producto concreto crean su propio caso dentro
/// de una transacción y hacen rollback; no dependen de seeds ni dejan datos.
/// </para>
/// </remarks>
public class CatalogTranslationTests
{
    private static CatalogDbContext? Abrir()
    {
        DotEnv.Load();
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            return null;
        }

        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(cadena).Options;
        return new CatalogDbContext(options, new NodeIdentity(NodeIdentity.DefaultCode), TimeProvider.System);
    }

    /// <summary>Abre la base, o salta la prueba diciendo por qué.</summary>
    private static async Task<CatalogDbContext?> AbrirODescartarAsync(CancellationToken ct)
    {
        var db = Abrir();

        if (db is null)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay base contra la que comprobar la traducción.");
            return null;
        }

        if (!await db.Database.CanConnectAsync(ct))
        {
            await db.DisposeAsync();
            Assert.Skip("La base no responde. ¿Está levantado 'docker compose up -d db'?");
            return null;
        }

        return db;
    }

    [Fact]
    public async Task Buscar_para_seleccion_se_traduce_y_se_ejecuta()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await AbrirODescartarAsync(ct);
        if (db is null) return;

        var servicio = new CatalogService(db);

        // **La afirmación es que no revienta al traducir.** La consulta lleva
        // dos subconsultas de colección dentro del `Select` —los precios de las
        // presentaciones y las categorías—, que es la forma exacta que EF Core
        // rechaza cuando está mal escrita.
        var resultados = await servicio.BuscarParaSeleccionAsync("cuaderno", 10, ct);

        Assert.NotNull(resultados);

        // Y las invariantes que no dependen de qué haya sembrado: sea cual sea
        // el catálogo, un resultado tiene identidad y nombre.
        foreach (var item in resultados)
        {
            Assert.NotEqual(Guid.Empty, item.ProductId);
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
            Assert.False(string.IsNullOrWhiteSpace(item.Slug));
        }
    }

    [Fact]
    public async Task Obtener_para_seleccion_se_traduce_y_devuelve_lo_mismo_que_buscar()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await AbrirODescartarAsync(ct);
        if (db is null) return;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        // La puerta migra una base vacía y deliberadamente no ejecuta seeds.
        // Esta prueba crea exactamente el dato que necesita, lo consulta por los
        // dos caminos y lo revierte al final: cero dependencia del catálogo demo.
        var producto = new Domain.Product
        {
            Name = $"Cuaderno para prueba {Guid.NewGuid():N}",
            Slug = $"cuaderno-prueba-{Guid.NewGuid():N}",
            ListPrice = 12.50m,
        };
        producto.Items.Add(new Domain.ProductItem { VariantValue = null, SortOrder = 0 });
        db.Products.Add(producto);
        await db.SaveChangesAsync(ct);

        var servicio = new CatalogService(db);
        var encontrados = await servicio.BuscarParaSeleccionAsync("cuaderno", 50, ct);
        var uno = Assert.Single(encontrados.Where(item => item.ProductId == producto.Id));
        var releido = await servicio.ObtenerParaSeleccionAsync(producto.Id, ct);

        // **Los dos caminos comparten composición, así que tienen que dar lo
        // mismo.** Si alguien los separa, esto se pone rojo — que es de lo que
        // sirve tenerlos juntos.
        Assert.Equal(uno, releido);

        await transaccion.RollbackAsync(ct);
    }

    [Fact]
    public async Task Un_producto_de_baja_no_se_puede_elegir_pero_si_releer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await AbrirODescartarAsync(ct);
        if (db is null) return;

        // **Se crea el caso y se revierte.** La base de desarrollo no tiene
        // ningún producto de baja, así que buscar uno haría que esta prueba se
        // saltara siempre — y una prueba que nunca corre no prueba nada. Dentro
        // de una transacción que no se confirma: la consulta lo ve porque
        // comparte conexión, y al terminar no queda ni rastro.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        var nombre = $"Producto de baja para prueba {Guid.NewGuid():N}";
        var producto = new Domain.Product
        {
            Name = nombre,
            Slug = $"producto-de-baja-prueba-{Guid.NewGuid():N}",
            IsActive = false,
            ListPrice = 9.90m,
        };

        db.Products.Add(producto);
        await db.SaveChangesAsync(ct);

        var deBaja = new { producto.Id, Name = nombre };
        var servicio = new CatalogService(db);

        // **La asimetría, afirmada.** Sin esto parece un descuido dentro de un
        // mes: buscar esconde las bajas —no se elige lo que no se vende— y
        // releer las devuelve marcadas, porque quien ya eligió necesita saber
        // que lo dieron de baja y no que desapareció.
        var releido = await servicio.ObtenerParaSeleccionAsync(deBaja.Id, ct);

        Assert.NotNull(releido);
        Assert.False(releido.IsActive);

        var buscado = await servicio.BuscarParaSeleccionAsync("baja prueba", 50, ct);
        Assert.DoesNotContain(buscado, item => item.ProductId == deBaja.Id);

        await transaccion.RollbackAsync(ct);
    }

    [Fact]
    public async Task Un_producto_que_no_existe_se_relee_como_nulo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await AbrirODescartarAsync(ct);
        if (db is null) return;

        var servicio = new CatalogService(db);

        // Nulo es la respuesta documentada, no un error: le dice a quien guarda
        // un snapshot que ya no hay nada detrás.
        Assert.Null(await servicio.ObtenerParaSeleccionAsync(Guid.NewGuid(), ct));
    }

    [Fact]
    public async Task La_busqueda_no_distingue_tildes_ni_mayusculas()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await AbrirODescartarAsync(ct);
        if (db is null) return;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        var producto = new Domain.Product
        {
            Name = $"LÁPIZ para prueba {Guid.NewGuid():N}",
            Slug = $"lapiz-prueba-{Guid.NewGuid():N}",
            ListPrice = 2.50m,
        };
        producto.Items.Add(new Domain.ProductItem { VariantValue = null, SortOrder = 0 });
        db.Products.Add(producto);
        await db.SaveChangesAsync(ct);

        var servicio = new CatalogService(db);

        var conTilde = await servicio.BuscarParaSeleccionAsync("LÁPIZ", 50, ct);
        var sinTilde = await servicio.BuscarParaSeleccionAsync("lapiz", 50, ct);

        // No va por la colación —PostgreSQL no admite estas operaciones sobre
        // `core.es_search`— sino por `spanish_stem` en el índice GIN. El efecto
        // observable es el mismo y es lo que se afirma. El producto propio
        // garantiza que la comparación nunca sea vacía.
        Assert.Contains(conTilde, p => p.ProductId == producto.Id);
        Assert.Contains(sinTilde, p => p.ProductId == producto.Id);
        Assert.Equal(
            conTilde.Select(p => p.ProductId).OrderBy(id => id),
            sinTilde.Select(p => p.ProductId).OrderBy(id => id));

        await transaccion.RollbackAsync(ct);
    }
}
