using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// El año de la serie y la guarda de transacción del numerador.
/// </summary>
/// <remarks>
/// Ninguna toca la base. El año es una conversión pura, y la guarda de transacción
/// se dispara <b>antes</b> de abrir conexión: leer
/// <c>Database.CurrentTransaction</c> no consulta nada. Así las dos barreras se
/// provocan en milisegundos, que es lo que hace que alguien las provoque.
/// </remarks>
public sealed class NumeracionTests
{
    // ================================================================
    // El año visible es el de Lima, no el de UTC.
    // ================================================================

    [Fact]
    public void El_anio_de_la_serie_es_el_de_Lima_y_no_el_de_UTC()
    {
        // 31 de diciembre de 2026, 20:00 en Lima (UTC-5) = 1 de enero de 2027, 01:00
        // en UTC. El pedido de esa tarde pertenece a la serie de 2026, que es el año
        // que el cliente tiene en su calendario.
        var nocheVieja = new DateTimeOffset(2027, 1, 1, 1, 0, 0, TimeSpan.Zero);

        Assert.Equal(2027, nocheVieja.UtcDateTime.Year);   // lo que decía antes
        Assert.Equal(2026, OrderCode.AnioDe(nocheVieja));  // lo que dice ahora
    }

    [Fact]
    public void Un_pedido_de_la_tarde_del_31_de_diciembre_conserva_su_anio()
    {
        // La frontera completa: los dos lados del cambio de año en Lima.
        var antes = new DateTimeOffset(2026, 12, 31, 23, 59, 0, TimeSpan.FromHours(-5));
        var despues = new DateTimeOffset(2027, 1, 1, 0, 1, 0, TimeSpan.FromHours(-5));

        Assert.Equal(2026, OrderCode.AnioDe(antes));
        Assert.Equal(2027, OrderCode.AnioDe(despues));
    }

    [Fact]
    public void El_codigo_de_esa_tarde_se_compone_con_el_anio_de_Lima()
    {
        var nocheVieja = new DateTimeOffset(2027, 1, 1, 1, 0, 0, TimeSpan.Zero);

        Assert.Equal("P-2026-0147", OrderCode.Componer("P", OrderCode.AnioDe(nocheVieja), 147));
    }

    [Fact]
    public void El_mediodia_de_Lima_y_el_de_UTC_dan_el_mismo_anio()
    {
        // El caso que no falla, para que la barrera no solo sepa decir no: si solo
        // se probara la frontera, una conversión que devolviera siempre 2026 pasaría.
        var mediodia = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(2026, OrderCode.AnioDe(mediodia));
        Assert.Equal(mediodia.UtcDateTime.Year, OrderCode.AnioDe(mediodia));
    }

    [Fact]
    public void La_zona_del_negocio_se_resuelve_en_esta_maquina()
    {
        // El desarrollo alterna entre Windows y Arch Linux, y el identificador es
        // IANA. Si la zona no se resolviera, el módulo no arrancaría: mejor saberlo
        // aquí que en el primer pedido.
        Assert.Equal("America/Lima", OrderCode.ZonaDelNegocio);
        Assert.Equal(2026, OrderCode.AnioDe(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    // ================================================================
    // La guarda de transacción.
    // ================================================================

    private static OrderCodeAllocator SinTransaccion()
    {
        // Contexto sin conexión abierta: la guarda dispara antes de necesitarla.
        var opciones = new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql("Host=localhost;Database=no_se_usa;Username=nadie;Password=nada")
            .Options;

        var db = new SalesDbContext(
            opciones, new NodeIdentity("principal"), TimeProvider.System);

        return new OrderCodeAllocator(
            db, new EtiquetaConfigurada("P"), new NodeIdentity("principal"), TimeProvider.System);
    }

    [Fact]
    public async Task Fuera_de_una_transaccion_el_numerador_se_niega_a_numerar()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SinTransaccion().SiguienteAsync(TestContext.Current.CancellationToken));

        Assert.Contains("transacción", error.Message, StringComparison.Ordinal);
        Assert.Contains("hueco", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_guarda_de_transaccion_actua_antes_de_mirar_la_configuracion()
    {
        // Sin etiqueta de serie Y sin transacción, gana la transacción: la guarda va
        // primero a propósito, para que el mensaje señale la causa que el llamador
        // puede arreglar sin tocar la instalación.
        var opciones = new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql("Host=localhost;Database=no_se_usa;Username=nadie;Password=nada")
            .Options;

        var db = new SalesDbContext(opciones, new NodeIdentity("principal"), TimeProvider.System);
        var sinEtiqueta = new OrderCodeAllocator(
            db, new EtiquetaConfigurada(null), new NodeIdentity("principal"), TimeProvider.System);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sinEtiqueta.SiguienteAsync(TestContext.Current.CancellationToken));

        Assert.Contains("transacción", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SalesSettingsKeys.OrderSeriesLabel, error.Message, StringComparison.Ordinal);
    }

    /// <summary>Lector de configuración mínimo: solo la etiqueta de serie.</summary>
    private sealed class EtiquetaConfigurada(string? etiqueta) : ISettingsReader
    {
        public string? Get(string key)
            => key == SalesSettingsKeys.OrderSeriesLabel ? etiqueta : null;

        public T? Get<T>(string key) => default;

        public IReadOnlyDictionary<string, string> GetPublic()
            => new Dictionary<string, string>();
    }
}
