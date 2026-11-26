using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// <b>R10-E13-D, R-1: con el bloqueo de moneda puesto, el pedido no lee la configuración.</b>
/// </summary>
/// <remarks>
/// <para>
/// El numerador leía la etiqueta de serie de la caché de configuración con el bloqueo
/// compartido puesto. Si la caché estaba invalidada, recargaba con su propio ámbito y su
/// propio <c>CoreDbContext</c>: otra conexión del pool mientras la del pedido retenía el
/// bloqueo. Ahora la etiqueta se captura y se valida antes de abrir la transacción, y el
/// numerador la recibe como argumento.
/// </para>
/// <para>
/// <b>Sin PostgreSQL.</b> La base es la guionada de Ventas, y la caché es un doble que
/// anota cada lectura y cada recarga, y si ocurrió con el bloqueo retenido. Una recarga
/// del doble representa la conexión adicional que pediría la caché real. Estas pruebas
/// demuestran el orden de las llamadas; no acreditan conexiones reales de PostgreSQL.
/// </para>
/// </remarks>
public sealed class SerieCapturadaAntesDelBloqueoTests
{
    private const string Bloqueo = "pg_advisory_xact_lock_shared(";
    private const string Serie = "\"sales\".\"order_series\"";
    private const string CargaDeAjustes = "AJUSTES: RECARGA CON OTRA CONEXIÓN";
    private const string MonedaPrevia = "MONEDA: LECTURA PREVIA";
    private const string MonedaEnTransaccion = "MONEDA: LECTURA EN LA TRANSACCIÓN";
    private static readonly string LecturaDeEtiqueta = "AJUSTE: " + SalesSettingsKeys.OrderSeriesLabel;
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Item = Guid.CreateVersion7();

    // ============================================================ captura anticipada

    [Fact]
    public async Task La_etiqueta_se_lee_antes_de_abrir_la_transaccion_y_de_tomar_el_bloqueo()
    {
        var ajustes = new AjustesObservados("P");
        var m = Montar(ajustes);

        await CrearHastaElGuardado(m);

        var pasos = m.Base.Pasos;
        var etiqueta = pasos.IndexOf(LecturaDeEtiqueta);
        var begin = pasos.IndexOf("BEGIN");
        var bloqueo = pasos.FindIndex(p => p.Contains(Bloqueo, StringComparison.Ordinal));

        Assert.True(etiqueta >= 0 && etiqueta < begin && begin < bloqueo,
            $"La etiqueta se lee antes del BEGIN y del bloqueo. Pasos: {string.Join(" | ", pasos)}");
        Assert.Single(ajustes.Lecturas, l => l.Clave == SalesSettingsKeys.OrderSeriesLabel);
    }

    [Fact]
    public async Task La_etiqueta_capturada_es_la_que_numera_aunque_la_configuracion_cambie_despues()
    {
        // Un cambio administrativo mientras el pedido está en curso no lo alcanza.
        var ajustes = new AjustesObservados("P");
        var m = Montar(ajustes, alEjecutar: sql =>
        {
            if (sql.Contains(Bloqueo, StringComparison.Ordinal))
            {
                ajustes.Etiqueta = "Q";
            }
        });

        await CrearHastaElGuardado(m);

        Assert.Equal("P-2026-0007", CodigoInsertado(m.Base));
    }

    [Fact]
    public async Task Una_cache_invalidada_antes_de_la_captura_recarga_antes_del_bloqueo_y_da_la_etiqueta_vigente()
    {
        var ajustes = new AjustesObservados("Q");
        ajustes.Invalidar();
        var m = Montar(ajustes);

        await CrearHastaElGuardado(m);

        var pasos = m.Base.Pasos;
        Assert.Equal(1, ajustes.Cargas);
        Assert.Equal(0, ajustes.CargasBajoBloqueo);
        Assert.True(pasos.IndexOf(CargaDeAjustes) < pasos.IndexOf("BEGIN"),
            $"La recarga ocurre antes de abrir la transacción. Pasos: {string.Join(" | ", pasos)}");
        Assert.Equal("Q-2026-0007", CodigoInsertado(m.Base));
    }

    [Fact]
    public async Task Una_cache_invalidada_con_el_bloqueo_retenido_no_se_recarga_durante_el_pedido()
    {
        // El caso de R-1: la caché se invalida justo cuando el pedido ya tiene el bloqueo.
        var ajustes = new AjustesObservados("P");
        var m = Montar(ajustes, alEjecutar: sql =>
        {
            if (sql.Contains(Bloqueo, StringComparison.Ordinal))
            {
                ajustes.Etiqueta = "Q";
                ajustes.Invalidar();
            }
        });

        await CrearHastaElGuardado(m);

        Assert.Equal(0, ajustes.Cargas);
        Assert.Equal(0, ajustes.CargasBajoBloqueo);
        Assert.DoesNotContain(ajustes.Lecturas, l => l.BajoBloqueo);
        Assert.DoesNotContain(CargaDeAjustes, m.Base.Pasos);
        Assert.Equal("P-2026-0007", CodigoInsertado(m.Base));
    }

    [Fact]
    public async Task El_pedido_siguiente_obtiene_la_configuracion_vigente_al_empezar_el_suyo()
    {
        // La captura es por operación, no una caché permanente.
        var ajustes = new AjustesObservados("P");
        var primero = Montar(ajustes, alEjecutar: sql =>
        {
            if (sql.Contains(Bloqueo, StringComparison.Ordinal))
            {
                ajustes.Etiqueta = "Q";
                ajustes.Invalidar();
            }
        });
        await CrearHastaElGuardado(primero);

        var segundo = Montar(ajustes);
        await CrearHastaElGuardado(segundo);

        Assert.Equal("P-2026-0007", CodigoInsertado(primero.Base));
        Assert.Equal("Q-2026-0007", CodigoInsertado(segundo.Base));
        Assert.Equal(1, ajustes.Cargas);
        Assert.Equal(0, ajustes.CargasBajoBloqueo);
        Assert.True(segundo.Base.Pasos.IndexOf(CargaDeAjustes) < segundo.Base.Pasos.IndexOf("BEGIN"));
    }

    // ============================================================ nada bajo el bloqueo

    [Fact]
    public async Task Dentro_de_la_transaccion_no_se_consulta_ningun_ajuste()
    {
        var ajustes = new AjustesObservados("P");
        var m = Montar(ajustes);

        await CrearHastaElGuardado(m);

        // Solo dos lecturas, las dos fuera: el plazo de pago y la etiqueta de serie.
        Assert.Equal(
            [SalesSettingsKeys.PaymentDueHours, SalesSettingsKeys.OrderSeriesLabel],
            ajustes.Lecturas.Select(l => l.Clave));
        Assert.DoesNotContain(ajustes.Lecturas, l => l.EnTransaccion);
        Assert.DoesNotContain(ajustes.Lecturas, l => l.BajoBloqueo);
    }

    [Fact]
    public async Task Bajo_el_bloqueo_solo_se_usa_la_conexion_y_la_transaccion_del_pedido()
    {
        var ajustes = new AjustesObservados("P");
        ajustes.Invalidar();
        var m = Montar(ajustes);

        await CrearHastaElGuardado(m);

        var pasos = m.Base.Pasos;
        var bloqueo = pasos.FindIndex(p => p.Contains(Bloqueo, StringComparison.Ordinal));
        var fin = pasos.LastIndexOf("ROLLBACK");
        var tramo = pasos.Skip(bloqueo).Take(fin - bloqueo + 1).ToList();

        // Ni recarga de la configuración ni lectura con la conexión de CORE en el tramo.
        Assert.DoesNotContain(CargaDeAjustes, tramo);
        Assert.DoesNotContain(MonedaPrevia, tramo);
        Assert.Equal(0, ajustes.CargasBajoBloqueo);

        // La lectura de moneda del tramo usa la misma transacción y la misma conexión.
        var transaccion = Assert.Single(m.Base.Transacciones);
        var (vista, conexion) = Assert.Single(m.Moneda.EnTransaccion);
        Assert.Same(transaccion, vista);
        Assert.Same(m.Base.Conexion, conexion);
    }

    [Fact]
    public async Task El_orden_ratificado_se_conserva_con_la_etiqueta_delante()
    {
        // Compatibilidad con E13-A y E13-B: la etiqueta se añade antes; lo demás no se mueve.
        var m = Montar(new AjustesObservados("P"));

        await CrearHastaElGuardado(m);

        var pasos = m.Base.Pasos;
        int Indice(Func<string, bool> es) => pasos.FindIndex(p => es(p));

        var orden = new[]
        {
            Indice(p => p == MonedaPrevia),
            Indice(p => p == LecturaDeEtiqueta),
            Indice(p => p == "BEGIN"),
            Indice(p => p.Contains(Bloqueo, StringComparison.Ordinal)),
            Indice(p => p == MonedaEnTransaccion),
            Indice(p => p.Contains(Serie, StringComparison.Ordinal)),
            Indice(EsInsercionDelPedido),
            pasos.LastIndexOf("ROLLBACK"),
        };

        Assert.All(orden, i => Assert.True(i >= 0, $"Falta un paso. Pasos: {string.Join(" | ", pasos)}"));
        Assert.Equal(orden.Order(), orden);
    }

    // ============================================================ rechazos seguros

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task Sin_etiqueta_valida_no_se_abre_transaccion_ni_se_toma_el_bloqueo_ni_se_numera(string? etiqueta)
    {
        var m = Montar(new AjustesObservados(etiqueta));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Crear(m));

        // La misma negativa del numerador, sin valor por defecto.
        Assert.Contains(SalesSettingsKeys.OrderSeriesLabel, error.Message, StringComparison.Ordinal);
        Assert.Contains("No tiene valor por defecto", error.Message, StringComparison.Ordinal);

        var pasos = m.Base.Pasos;
        Assert.DoesNotContain("BEGIN", pasos);
        Assert.DoesNotContain(pasos, p => p.Contains(Bloqueo, StringComparison.Ordinal));
        Assert.DoesNotContain(pasos, p => p.Contains(Serie, StringComparison.Ordinal));
        Assert.DoesNotContain(pasos, EsInsercionDelPedido);
        Assert.Empty(m.Moneda.EnTransaccion);
        Assert.Empty(m.Base.Transacciones);
    }

    [Fact]
    public async Task Un_fallo_al_guardar_deshace_el_numero_y_no_deja_pedido()
    {
        var m = Montar(new AjustesObservados("P"));

        await CrearHastaElGuardado(m);

        var pasos = m.Base.Pasos;
        var numero = pasos.FindIndex(p => p.Contains(Serie, StringComparison.Ordinal));
        var insercion = pasos.FindIndex(EsInsercionDelPedido);

        // El número se reservó en la transacción del pedido, que se deshizo entera.
        Assert.True(numero >= 0 && numero < insercion);
        Assert.Equal("ROLLBACK", pasos[^1]);
        Assert.DoesNotContain("COMMIT", pasos);
        Assert.Single(m.Base.Transacciones);
    }

    [Fact]
    public async Task Sin_moneda_acreditada_se_rechaza_antes_de_leer_la_etiqueta()
    {
        // D20-A: la moneda va primero. Con moneda ausente y etiqueta ausente gana la
        // moneda, y no se lee ningún ajuste ni se abre nada.
        var ajustes = new AjustesObservados(null);
        var m = Montar(ajustes, moneda: null);

        var resultado = await Crear(m);

        Assert.Equal(MotivoDeNoCreacion.MonedaSinAutoridad, resultado.Motivo);
        Assert.Empty(ajustes.Lecturas);
        Assert.DoesNotContain("BEGIN", m.Base.Pasos);
        Assert.Empty(m.Moneda.EnTransaccion);
    }

    // ============================================================ regresión estructural

    [Fact]
    public void El_numerador_no_depende_de_la_configuracion()
    {
        var parametros = typeof(OrderCodeAllocator).GetConstructors().Single().GetParameters()
            .Select(p => p.ParameterType)
            .ToArray();

        Assert.Equal([typeof(SalesDbContext), typeof(NodeIdentity), typeof(TimeProvider)], parametros);
    }

    [Fact]
    public void El_numerador_recibe_la_etiqueta_como_argumento()
    {
        var siguiente = typeof(OrderCodeAllocator).GetMethod(nameof(OrderCodeAllocator.SiguienteAsync))!;

        Assert.Equal(
            [typeof(string), typeof(CancellationToken)],
            siguiente.GetParameters().Select(p => p.ParameterType));
        Assert.DoesNotContain(
            typeof(OrderCodeAllocator).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            f => typeof(ISettingsReader).IsAssignableFrom(f.FieldType)
                || f.FieldType == typeof(IServiceProvider)
                || f.FieldType.Name.Contains("ScopeFactory", StringComparison.Ordinal));
    }

    [Fact]
    public void El_codigo_del_numerador_no_consulta_la_configuracion_ni_abre_ambitos()
    {
        var codigo = SoloCodigo("Sillar.Modules.Sales/Data/OrderCodeAllocator.cs");

        foreach (var prohibido in new[]
                 {
                     "ISettingsReader", "SettingsCache", "SiteSetting", "CoreDbContext",
                     "IServiceScopeFactory", "IServiceProvider", "CreateScope", "GetRequiredService",
                     ".Get(", ".Get<", "settings",
                 })
        {
            Assert.DoesNotContain(prohibido, codigo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void En_el_creador_toda_lectura_de_ajustes_va_antes_de_abrir_la_transaccion()
    {
        var codigo = SoloCodigo("Sillar.Modules.Sales/Pedidos/CreadorDePedidos.cs");

        var transaccion = codigo.IndexOf("TransaccionDeOperacion.AbrirSiHaceFaltaAsync", StringComparison.Ordinal);
        var lecturas = Regex.Matches(codigo, @"\bajustes\.").Select(x => x.Index).ToList();

        Assert.True(transaccion > 0);
        Assert.Equal(2, lecturas.Count);
        Assert.All(lecturas, i => Assert.True(i < transaccion, "un ajuste leído después de abrir la transacción"));
        Assert.Contains("numerador.EtiquetaValida(ajustes.Get(SalesSettingsKeys.OrderSeriesLabel))", codigo, StringComparison.Ordinal);
        Assert.Contains("numerador.SiguienteAsync(etiquetaDeSerie, cancellationToken)", codigo, StringComparison.Ordinal);
        Assert.True(codigo.IndexOf("numerador.EtiquetaValida", StringComparison.Ordinal) < transaccion);
    }

    // ============================================================ montaje

    private sealed record Montaje(BaseGuionadaDeVentas Base, CreadorDePedidos Creador, MonedaAnotada Moneda);

    private static Montaje Montar(AjustesObservados ajustes, string? moneda = "PEN", Action<string>? alEjecutar = null)
    {
        var base_ = new BaseGuionadaDeVentas
        {
            Responde = sql => sql.Contains("current_setting('lock_timeout')", StringComparison.Ordinal)
                ? BaseGuionadaDeVentas.Escalar("Value", "0")
                : null,
            RespondeEscalar = sql => sql.Contains(Serie, StringComparison.Ordinal) ? 7 : null,
            FallaAlEjecutar = sql =>
            {
                alEjecutar?.Invoke(sql);
                return EsInsercionDelPedido(sql) ? new GuardadoDetenido() : null;
            },
        };
        ajustes.Base = base_;
        var contexto = base_.Contexto();
        var monedas = new MonedaAnotada(base_, moneda);

        var creador = new CreadorDePedidos(
            contexto,
            new CongeladorDeCliente(new Dobles.M04Dice(Dobles.Cliente(Guid.CreateVersion7()))),
            new Dobles.CatalogoDice().Con(Item, activo: true, precio: 8.50m),
            new OrderCodeAllocator(contexto, new NodeIdentity("prueba"), new Dobles.RelojFijo(Ahora)),
            ajustes,
            monedas,
            new Dobles.RelojFijo(Ahora),
            NullLogger<CreadorDePedidos>.Instance);

        return new Montaje(base_, creador, monedas);
    }

    private static Task<ResultadoDeCreacion> Crear(Montaje m)
        => m.Creador.CrearAsync(Guid.CreateVersion7(), [new LineaPedida(Item, 1)], TestContext.Current.CancellationToken);

    /// <summary>
    /// Recorre el pedido entero hasta la inserción, que la base guionada detiene: sin
    /// servidor, EF no puede leer lo que devolvería el <c>INSERT</c>. Lo que se mira es
    /// todo lo anterior al <c>COMMIT</c>.
    /// </summary>
    private static async Task CrearHastaElGuardado(Montaje m)
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => Crear(m));
        Assert.IsType<GuardadoDetenido>(error.InnerException);
    }

    private static bool EsInsercionDelPedido(string sql)
        => Regex.IsMatch(sql, @"INSERT INTO ""?sales""?\.""?orders""?\s*\(");

    private static string CodigoInsertado(BaseGuionadaDeVentas base_)
    {
        var insercion = Assert.Single(base_.Comandos, c => EsInsercionDelPedido(c.Texto));
        return Assert.Single(
            insercion.Valores.Values.OfType<string>(),
            v => Regex.IsMatch(v, @"^[A-Z]+-\d{4}-\d{4,}$"));
    }

    /// <summary>Si el bloqueo compartido está tomado y su transacción no ha terminado.</summary>
    private static bool BloqueoRetenido(BaseGuionadaDeVentas? base_)
    {
        if (base_ is null)
        {
            return false;
        }

        var bloqueo = base_.Pasos.FindLastIndex(p => p.Contains(Bloqueo, StringComparison.Ordinal));
        var fin = base_.Pasos.FindLastIndex(p => p is "COMMIT" or "ROLLBACK");
        return bloqueo >= 0 && fin < bloqueo;
    }

    private static bool TransaccionAbierta(BaseGuionadaDeVentas? base_)
    {
        if (base_ is null)
        {
            return false;
        }

        var begin = base_.Pasos.LastIndexOf("BEGIN");
        var fin = base_.Pasos.FindLastIndex(p => p is "COMMIT" or "ROLLBACK");
        return begin >= 0 && fin < begin;
    }

    /// <summary>Lo que lanza la base guionada al insertar el pedido.</summary>
    private sealed class GuardadoDetenido() : Exception("La base guionada no inserta pedidos.");

    /// <summary>
    /// La caché de configuración, observada. Como la real, una lectura con la caché
    /// invalidada recarga; aquí, además, la recarga queda anotada con el momento en que
    /// ocurrió, porque es la que pediría otra conexión del pool.
    /// </summary>
    private sealed class AjustesObservados(string? etiqueta) : ISettingsReader
    {
        private bool invalidada;

        public string? Etiqueta { get; set; } = etiqueta;

        public BaseGuionadaDeVentas? Base { get; set; }

        public List<(string Clave, bool EnTransaccion, bool BajoBloqueo)> Lecturas { get; } = [];

        public int Cargas { get; private set; }

        public int CargasBajoBloqueo { get; private set; }

        public void Invalidar() => invalidada = true;

        public string? Get(string key)
        {
            Anotar(key);
            return key == SalesSettingsKeys.OrderSeriesLabel ? Etiqueta : null;
        }

        public T? Get<T>(string key)
        {
            Anotar(key);
            return key == SalesSettingsKeys.PaymentDueHours && 48 is T horas ? horas : default;
        }

        public IReadOnlyDictionary<string, string> GetPublic()
        {
            Anotar("*");
            return new Dictionary<string, string>();
        }

        private void Anotar(string clave)
        {
            var bajoBloqueo = BloqueoRetenido(Base);

            if (invalidada)
            {
                invalidada = false;
                Cargas++;
                if (bajoBloqueo)
                {
                    CargasBajoBloqueo++;
                }

                Base?.Pasos.Add(CargaDeAjustes);
            }

            Lecturas.Add((clave, TransaccionAbierta(Base), bajoBloqueo));
            Base?.Pasos.Add("AJUSTE: " + clave);
        }
    }

    /// <summary>La autoridad de moneda, que anota cada lectura y la conexión con que llegó.</summary>
    private sealed class MonedaAnotada(BaseGuionadaDeVentas base_, string? codigo) : IAutoridadDeMoneda
    {
        public List<(DbTransaction Transaccion, DbConnection? Conexion)> EnTransaccion { get; } = [];

        public Task<LecturaDeMonedaDeVenta> MonedaDeVentaAsync(CancellationToken cancellationToken)
        {
            base_.Pasos.Add(MonedaPrevia);
            return Task.FromResult(Lectura());
        }

        public Task<LecturaDeMonedaDeVenta> LeerEnTransaccionAsync(DbTransaction transaccion, CancellationToken cancellationToken)
        {
            base_.Pasos.Add(MonedaEnTransaccion);
            EnTransaccion.Add((transaccion, transaccion.Connection));
            return Task.FromResult(Lectura());
        }

        private LecturaDeMonedaDeVenta Lectura()
            => codigo is null ? LecturaDeMonedaDeVenta.SinAutoridad : LecturaDeMonedaDeVenta.Acreditada(codigo);
    }

    // ============================================================ fuentes

    private static string SoloCodigo(string relativa)
    {
        var ruta = Path.Combine(Backend(), relativa.Replace('/', Path.DirectorySeparatorChar));
        return string.Join('\n', File.ReadAllLines(ruta)
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    private static string Backend()
    {
        for (var directorio = new DirectoryInfo(AppContext.BaseDirectory); directorio is not null; directorio = directorio.Parent)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "backend", "Sillar.sln")))
            {
                return Path.Combine(directorio.FullName, "backend");
            }
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
