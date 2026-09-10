using Microsoft.EntityFrameworkCore;
using Sillar.Shared.Data.Replication;
using Sillar.Shared.Replication;

namespace Sillar.Shared.Data.Tests;

/// <summary>
/// Una fila replicada de mentira, con lo justo para tener rastreador.
/// </summary>
internal sealed class FilaReplicada : IReplicatedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Nombre { get; set; } = string.Empty;

    public string OriginNode { get; set; } = string.Empty;
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Contexto de prueba. Apunta a un servidor que no existe: si alguna de estas
/// pruebas intentara hablar con una base de datos, fallaría al conectar en vez
/// de pasar en silencio.
/// </summary>
internal sealed class ContextoDePrueba : DbContext
{
    public DbSet<FilaReplicada> Filas => Set<FilaReplicada>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseNpgsql("Host=no.existe;Database=ninguna;Username=nadie;Password=ninguna");

    protected override void OnModelCreating(ModelBuilder model)
        => model.Entity<FilaReplicada>().HasKey(x => x.Id);
}

public sealed class ReplicationStampingTests
{
    private static readonly NodeIdentity Nodo = new("sucursal-2");
    private static readonly DateTimeOffset Momento = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static (ContextoDePrueba contexto, FakeTimeProvider reloj) Preparar()
        => (new ContextoDePrueba(), new FakeTimeProvider(Momento));

    [Fact]
    public void Una_fila_nueva_nace_con_el_nodo_la_version_uno_y_las_dos_fechas()
    {
        var (contexto, reloj) = Preparar();
        var fila = new FilaReplicada { Nombre = "alta" };
        contexto.Add(fila);

        contexto.ChangeTracker.StampReplicationColumns(Nodo, reloj);

        Assert.Equal("sucursal-2", fila.OriginNode);
        Assert.Equal(1, fila.RowVersion);
        Assert.Equal(Momento, fila.CreatedAt);
        Assert.Equal(Momento, fila.UpdatedAt);
    }

    [Fact]
    public void Al_modificar_sube_la_version_y_el_origen_no_se_toca()
    {
        var (contexto, reloj) = Preparar();
        var fila = new FilaReplicada { Nombre = "vieja", OriginNode = "principal", RowVersion = 7 };
        contexto.Attach(fila);
        fila.Nombre = "editada";

        contexto.ChangeTracker.StampReplicationColumns(Nodo, reloj);

        Assert.Equal("principal", fila.OriginNode);
        Assert.Equal(8, fila.RowVersion);
    }

    [Fact]
    public void Al_modificar_no_se_reescriben_ni_el_origen_ni_la_fecha_de_alta()
    {
        var (contexto, reloj) = Preparar();
        var fila = new FilaReplicada { Nombre = "vieja", OriginNode = "principal", RowVersion = 1 };
        contexto.Attach(fila);
        fila.Nombre = "editada";

        contexto.ChangeTracker.StampReplicationColumns(Nodo, reloj);

        var entrada = contexto.Entry(fila);
        Assert.False(entrada.Property(nameof(IReplicatedEntity.OriginNode)).IsModified);
        Assert.False(entrada.Property(nameof(IReplicatedEntity.CreatedAt)).IsModified);
    }

    [Fact]
    public void Una_fila_sin_cambios_no_se_sella()
    {
        var (contexto, reloj) = Preparar();
        var fila = new FilaReplicada { Nombre = "intacta", OriginNode = "principal", RowVersion = 3 };
        contexto.Attach(fila);

        contexto.ChangeTracker.StampReplicationColumns(Nodo, reloj);

        Assert.Equal(3, fila.RowVersion);
        Assert.Equal("principal", fila.OriginNode);
    }

    [Fact]
    public void El_borrado_tampoco_sube_la_version()
    {
        var (contexto, reloj) = Preparar();
        var fila = new FilaReplicada { Nombre = "de baja", OriginNode = "principal", RowVersion = 4 };
        contexto.Attach(fila);
        contexto.Remove(fila);

        contexto.ChangeTracker.StampReplicationColumns(Nodo, reloj);

        Assert.Equal(4, fila.RowVersion);
    }
}

/// <summary>
/// Reloj fijo. El sellado toma la hora del <c>TimeProvider</c> que se le pasa, y
/// eso es lo que permite afirmar la fecha exacta en vez de un rango.
/// </summary>
internal sealed class FakeTimeProvider(DateTimeOffset momento) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => momento;
}
