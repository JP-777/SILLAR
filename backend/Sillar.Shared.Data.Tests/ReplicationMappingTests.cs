using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Sillar.Shared.Data.Replication;
using Sillar.Shared.Replication;

namespace Sillar.Shared.Data.Tests;

/// <summary>
/// Contexto que <b>sí</b> mapea la replicación, para poder mirar el modelo que
/// construye <see cref="ReplicationMapping.MapReplication{T}"/>.
/// </summary>
internal sealed class ContextoMapeado : DbContext
{
    public DbSet<FilaReplicada> Filas => Set<FilaReplicada>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseNpgsql("Host=no.existe;Database=ninguna;Username=nadie;Password=ninguna");

    protected override void OnModelCreating(ModelBuilder model)
    {
        var entidad = model.Entity<FilaReplicada>();
        entidad.HasKey(x => x.Id);
        entidad.MapReplication();
    }
}

/// <summary>
/// El mapeo de las columnas de replicación.
///
/// <para>
/// <b>Por qué existe esta clase entera.</b> El pendiente 3 pedía llevarse el
/// sellado <b>y</b> el mapeo, «a medias no», y la extracción cumplió esa
/// condición — pero las pruebas se quedaron a medias: cinco para el sellado y
/// ninguna para el mapeo. Es la misma frase aplicada un nivel más abajo.
/// </para>
/// <para>
/// <b>Y el mapeo es la mitad cuyo fallo se ve más tarde.</b> Si el sellado se
/// rompe, una fila sale con <c>origin_node</c> vacío y se nota mirándola. Si el
/// mapeo se rompe, EF cae en su convención y busca una columna
/// <c>OriginNode</c> que no existe: no falla la compilación, no falla la
/// migración —el esquema ya estaba creado— y falla una consulta cualquiera en
/// tiempo de ejecución, lejos de la causa.
/// </para>
/// <para>
/// Ninguna prueba de aquí abre una conexión: se mira el modelo que EF
/// construye, que es metadato. El contexto apunta a un servidor que no existe
/// a propósito, para que intentarlo fallara en vez de pasar en silencio.
/// </para>
/// </summary>
public sealed class ReplicationMappingTests
{
    private static IEntityType Entidad()
        => new ContextoMapeado().Model.FindEntityType(typeof(FilaReplicada))!;

    private static IProperty Propiedad(string nombre)
        => Entidad().FindProperty(nombre)!;

    [Fact]
    public void Las_cuatro_columnas_llevan_el_nombre_en_snake_case_del_esquema()
    {
        Assert.Equal("origin_node", Propiedad(nameof(IReplicatedEntity.OriginNode)).GetColumnName());
        Assert.Equal("row_version", Propiedad(nameof(IReplicatedEntity.RowVersion)).GetColumnName());
        Assert.Equal("created_at", Propiedad(nameof(IReplicatedEntity.CreatedAt)).GetColumnName());
        Assert.Equal("updated_at", Propiedad(nameof(IReplicatedEntity.UpdatedAt)).GetColumnName());
    }

    [Fact]
    public void Sin_MapReplication_EF_las_nombraria_de_otra_forma()
    {
        // **La prueba de que las cuatro de arriba comprueban algo.** Este es el
        // mismo tipo de entidad en un contexto que no mapea la replicación: EF
        // cae en su convención y la columna pasa a llamarse OriginNode. Sin
        // esta comparación, las aserciones anteriores podrían estar pasando por
        // una convención de nombres configurada en otro sitio, y no por
        // MapReplication — que es lo que dicen comprobar.
        var sinMapear = new ContextoDePrueba().Model.FindEntityType(typeof(FilaReplicada))!;

        Assert.Equal(
            nameof(IReplicatedEntity.OriginNode),
            sinMapear.FindProperty(nameof(IReplicatedEntity.OriginNode))!.GetColumnName());
    }

    [Fact]
    public void El_nodo_de_origen_es_obligatorio()
    {
        Assert.False(Propiedad(nameof(IReplicatedEntity.OriginNode)).IsNullable);
    }

    [Fact]
    public void La_version_nace_en_uno_y_no_la_genera_la_base()
    {
        var version = Propiedad(nameof(IReplicatedEntity.RowVersion));

        Assert.Equal(1L, version.GetDefaultValue());

        // Nunca generada: la escribe el sellado, no PostgreSQL. Si EF la diera
        // por generada, dejaría de mandar el valor incrementado y row_version
        // se quedaría clavada en el default.
        Assert.Equal(ValueGenerated.Never, version.ValueGenerated);
    }

    [Fact]
    public void La_fecha_de_alta_la_pone_la_base_y_solo_al_insertar()
    {
        var creada = Propiedad(nameof(IReplicatedEntity.CreatedAt));

        Assert.Equal("timestamptz", creada.GetColumnType());
        Assert.Equal("now()", creada.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.OnAdd, creada.ValueGenerated);
    }

    [Fact]
    public void La_fecha_de_cambio_la_pone_el_trigger_al_insertar_y_al_actualizar()
    {
        var cambiada = Propiedad(nameof(IReplicatedEntity.UpdatedAt));

        Assert.Equal("timestamptz", cambiada.GetColumnType());
        Assert.Equal("now()", cambiada.GetDefaultValueSql());

        // OnAddOrUpdate y no OnAdd: la escribe el trigger set_updated_at() del
        // schema del módulo en cada UPDATE, y EF tiene que volver a leerla para
        // que la entidad en memoria no se quede con la fecha vieja.
        Assert.Equal(ValueGenerated.OnAddOrUpdate, cambiada.ValueGenerated);
    }
}
