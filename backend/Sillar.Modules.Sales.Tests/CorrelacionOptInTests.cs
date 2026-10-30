using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// El registro opt-in de la correlación de pedidos, sin el resto del comercio candidato.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ninguna toca PostgreSQL.</b> Las de composición solo construyen el contenedor.
/// Las de semántica resuelven el lector real por DI y ejecutan su consulta EF real; lo
/// único sustituido es el transporte: <see cref="BaseSimulada"/> suprime la apertura de
/// la conexión y responde a la consulta con filas propias, comparando <b>ordinal</b>,
/// como hace la columna sin colación.
/// </para>
/// <para>
/// <b>Lo que estas pruebas NO prueban:</b> que PostgreSQL compare <c>order_code</c> byte
/// a byte. Eso sigue necesitando base, igual que en <see cref="CorrelacionDePedidoCandidataTests"/>.
/// </para>
/// </remarks>
public sealed class CorrelacionOptInTests
{
    private static readonly Guid Pedido = Guid.Parse("01999f3a-0000-7000-8000-000000000001");
    private static readonly Guid Otro = Guid.Parse("01999f3a-0000-7000-8000-000000000002");

    private const string Codigo = "P-2026-0147";

    private const string CadenaSinBase =
        "Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1";

    /// <summary>Los servicios que esta composición no debe arrastrar nunca.</summary>
    private static readonly Type[] Prohibidos =
    [
        typeof(IPayableOrderCandidate),
        typeof(IExternalSettlementCandidate),
        typeof(ISettlementCandidateJournal),
        typeof(CandidatePayableOrderReader),
        typeof(CandidateExternalSettlementApplicator),
        typeof(InProcessSettlementCandidateJournal),
    ];

    // ---------- composición ----------

    [Fact]
    public void T1_T14_Sin_pedirla_ni_SalesModule_ni_nadie_registra_la_correlacion()
    {
        var servicios = ServiciosDeM03();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IOrderCorrelationCandidate));

        using var proveedor = servicios.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();
        Assert.Null(ambito.ServiceProvider.GetService<IOrderCorrelationCandidate>());
    }

    [Fact]
    public void T2_Pedida_expresamente_resuelve_el_lector_real_de_M03()
    {
        var servicios = ServiciosDeM03().AddCandidateSalesOrderCorrelation();

        // Sin ValidateOnBuild: otros servicios de M03 piden contratos de CORE y M04 que
        // pone el host, y aquí solo importa que la correlación no los necesita.
        using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var ambito = proveedor.CreateScope();

        var correlacion = ambito.ServiceProvider.GetRequiredService<IOrderCorrelationCandidate>();
        Assert.IsType<CandidateOrderCorrelationReader>(correlacion);
    }

    [Fact]
    public void T3_T4_No_registra_pagabilidad_liquidacion_ni_memoria_de_reintentos()
    {
        var servicios = ServiciosDeM03().AddCandidateSalesOrderCorrelation();

        foreach (var tipo in Prohibidos)
        {
            Assert.DoesNotContain(servicios, d => d.ServiceType == tipo || d.ImplementationType == tipo);
        }

        using var proveedor = servicios.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();
        Assert.Null(ambito.ServiceProvider.GetService<IPayableOrderCandidate>());
        Assert.Null(ambito.ServiceProvider.GetService<IExternalSettlementCandidate>());
        Assert.Null(ambito.ServiceProvider.GetService<ISettlementCandidateJournal>());
    }

    [Fact]
    public void T5_Anade_un_unico_registro_y_no_reutiliza_el_bloque_comercial()
    {
        var servicios = ServiciosDeM03();
        var antes = servicios.ToList();

        servicios.AddCandidateSalesOrderCorrelation();

        var nuevo = Assert.Single(servicios.Except(antes));
        Assert.Equal(typeof(IOrderCorrelationCandidate), nuevo.ServiceType);
        Assert.Equal(typeof(CandidateOrderCorrelationReader), nuevo.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, nuevo.Lifetime);

        // Pedirla dos veces no duplica nada.
        servicios.AddCandidateSalesOrderCorrelation();
        Assert.Single(servicios, d => d.ServiceType == typeof(IOrderCorrelationCandidate));
    }

    [Fact]
    public void Sin_M03_registrado_la_correlacion_no_se_inventa_una_base()
    {
        var servicios = new ServiceCollection().AddCandidateSalesOrderCorrelation();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(SalesDbContext));

        using var proveedor = servicios.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();
        Assert.Throws<InvalidOperationException>(
            () => ambito.ServiceProvider.GetRequiredService<IOrderCorrelationCandidate>());
    }

    // ---------- semántica, por el lector real ----------

    [Fact]
    public async Task T6_Un_codigo_valido_se_resuelve_sin_leer_nada_comercial()
    {
        var (correlacion, base_, contexto) = Componer(new OrderIdentityRecord(Pedido, Codigo));

        var resultado = await correlacion.ResolveAsync(new OrderCorrelationRequest(Codigo, null), CancellationToken.None);

        Assert.Equal(
            new OrderCorrelationCandidate(OrderCorrelationOutcome.Resolved, Pedido, Codigo, CandidateSources.CurrentDatabase),
            resultado);

        // Solo identidad: la consulta no mira pagos, estado, total ni moneda.
        var consulta = Assert.Single(base_.Consultas);
        Assert.Contains("orders", consulta);
        Assert.DoesNotContain("order_payments", consulta);
        Assert.DoesNotContain("status", consulta);
        Assert.DoesNotContain("total_amount", consulta);
        Assert.DoesNotContain("currency_code", consulta);
        Assert.Empty(contexto.ChangeTracker.Entries());
    }

    [Fact]
    public async Task T6_Un_identificador_valido_se_resuelve_con_su_codigo_guardado()
    {
        var (correlacion, _, _) = Componer(new OrderIdentityRecord(Pedido, Codigo));

        var resultado = await correlacion.ResolveAsync(new OrderCorrelationRequest(null, Pedido), CancellationToken.None);

        Assert.Equal(OrderCorrelationOutcome.Resolved, resultado.Outcome);
        Assert.Equal(Pedido, resultado.OrderId);
        Assert.Equal(Codigo, resultado.OrderCode);
    }

    [Theory]
    [InlineData("P-2026-0148")]
    [InlineData("p-2026-0147")]
    public async Task T7_Un_codigo_bien_formado_sin_pedido_es_desconocido(string declarado)
    {
        var (correlacion, base_, _) = Componer(new OrderIdentityRecord(Pedido, Codigo));

        var resultado = await correlacion.ResolveAsync(new OrderCorrelationRequest(declarado, null), CancellationToken.None);

        Assert.Equal(
            new OrderCorrelationCandidate(OrderCorrelationOutcome.UnknownReference, null, null, CandidateSources.CurrentDatabase),
            resultado);
        Assert.Single(base_.Consultas);
    }

    [Theory]
    [InlineData("P-2026-147")]
    [InlineData("P20260147")]
    [InlineData("P-2026-014X")]
    public async Task T8_Un_codigo_malformado_no_consulta_ni_acredita_identidad(string declarado)
    {
        var (correlacion, base_, _) = Componer(new OrderIdentityRecord(Pedido, Codigo));

        var resultado = await correlacion.ResolveAsync(new OrderCorrelationRequest(declarado, Pedido), CancellationToken.None);

        Assert.Equal(
            new OrderCorrelationCandidate(OrderCorrelationOutcome.MalformedReference, null, null, CandidateSources.RatifiedRules),
            resultado);
        Assert.Empty(base_.Consultas);
    }

    [Fact]
    public async Task T8_Guid_vacio_es_malformado_y_no_consulta()
    {
        var (correlacion, base_, _) = Componer(new OrderIdentityRecord(Pedido, Codigo));

        var resultado = await correlacion.ResolveAsync(new OrderCorrelationRequest(null, Guid.Empty), CancellationToken.None);

        Assert.Equal(OrderCorrelationOutcome.MalformedReference, resultado.Outcome);
        Assert.Null(resultado.OrderId);
        Assert.Empty(base_.Consultas);
    }

    [Fact]
    public async Task T9_Codigo_e_identificador_que_no_concuerdan_son_divergentes()
    {
        var (correlacion, base_, _) = Componer(new OrderIdentityRecord(Pedido, Codigo));

        var resultado = await correlacion.ResolveAsync(new OrderCorrelationRequest(Codigo, Otro), CancellationToken.None);

        Assert.Equal(
            new OrderCorrelationCandidate(OrderCorrelationOutcome.DivergentDeclaration, null, null, CandidateSources.CurrentDatabase),
            resultado);
        Assert.Single(base_.Consultas);
    }

    [Fact]
    public async Task T9_Una_referencia_sin_verificar_no_es_autoritativa_ni_consulta()
    {
        var (correlacion, base_, _) = Componer(new OrderIdentityRecord(Pedido, Codigo));

        var resultado = await correlacion.ResolveAsync(
            new OrderCorrelationRequest(null, null) { UnverifiedProviderReference = Codigo },
            CancellationToken.None);

        Assert.Equal(
            new OrderCorrelationCandidate(OrderCorrelationOutcome.NotAuthoritative, null, null, CandidateSources.RatifiedRules),
            resultado);
        Assert.Empty(base_.Consultas);
    }

    [Fact]
    public async Task T10_Fuera_de_Resolved_no_aparece_ninguna_identidad()
    {
        var (correlacion, _, _) = Componer(new OrderIdentityRecord(Pedido, Codigo));

        OrderCorrelationRequest[] solicitudes =
        [
            new(null, null),
            new(null, null) { UnverifiedProviderReference = Codigo },
            new("P-2026-147", null),
            new(null, Guid.Empty),
            new("P-2026-0148", null),
            new(null, Otro),
            new(Codigo, Otro),
        ];

        foreach (var solicitud in solicitudes)
        {
            var resultado = await correlacion.ResolveAsync(solicitud, CancellationToken.None);

            Assert.NotEqual(OrderCorrelationOutcome.Resolved, resultado.Outcome);
            Assert.Null(resultado.OrderId);
            Assert.Null(resultado.OrderCode);
            Assert.Contains(resultado.Source, new[] { CandidateSources.CurrentDatabase, CandidateSources.RatifiedRules });
        }
    }

    [Fact]
    public async Task T11_La_cancelacion_se_propaga_y_no_se_vuelve_una_respuesta()
    {
        // Sin interceptor: la cancelación la ve la apertura real de la conexión.
        var correlacion = ResolverConContexto(Contexto(interceptor: null));
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => correlacion.ResolveAsync(new OrderCorrelationRequest(Codigo, null), cancelado.Token));
    }

    [Fact]
    public async Task T11_Una_base_inalcanzable_propaga_el_fallo_y_no_dice_desconocido()
    {
        var correlacion = ResolverConContexto(Contexto(interceptor: null));

        var fallo = await Record.ExceptionAsync(
            () => correlacion.ResolveAsync(new OrderCorrelationRequest(Codigo, null), CancellationToken.None));

        Assert.NotNull(fallo);
        Assert.IsNotType<OperationCanceledException>(fallo);
    }

    [Fact]
    public async Task T11_Dos_filas_para_una_clave_unica_es_una_averia_y_duele()
    {
        var (correlacion, _, _) = Componer(
            new OrderIdentityRecord(Pedido, Codigo),
            new OrderIdentityRecord(Otro, Codigo));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => correlacion.ResolveAsync(new OrderCorrelationRequest(Codigo, null), CancellationToken.None));
    }

    [Fact]
    public async Task T12_Resolver_solo_emite_lecturas_y_no_deja_nada_que_guardar()
    {
        var (correlacion, base_, contexto) = Componer(new OrderIdentityRecord(Pedido, Codigo));

        await correlacion.ResolveAsync(new OrderCorrelationRequest(Codigo, null), CancellationToken.None);
        await correlacion.ResolveAsync(new OrderCorrelationRequest(null, Pedido), CancellationToken.None);
        await correlacion.ResolveAsync(new OrderCorrelationRequest("P-2026-0148", null), CancellationToken.None);

        Assert.Equal(3, base_.Consultas.Count);
        Assert.All(base_.Consultas, sql => Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.Ordinal));
        Assert.Equal(0, base_.Escrituras);
        Assert.Empty(contexto.ChangeTracker.Entries());
        Assert.False(contexto.ChangeTracker.HasChanges());
    }

    // ---------- montaje ----------

    /// <summary>Lo que <c>SalesModule</c> registra en producción, sin pedir nada más.</summary>
    private static ServiceCollection ServiciosDeM03()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SalesModule.ConnectionStringName}"] = CadenaSinBase,
            })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddSingleton(TimeProvider.System);
        new SalesModule().RegisterServices(servicios, configuracion);
        return servicios;
    }

    /// <summary>
    /// Contenedor con la extensión nueva y un <see cref="SalesDbContext"/> cuyo transporte
    /// es <see cref="BaseSimulada"/>. El lector y su consulta son los reales.
    /// </summary>
    private static (IOrderCorrelationCandidate Correlacion, BaseSimulada Base, SalesDbContext Contexto) Componer(
        params OrderIdentityRecord[] filas)
    {
        var base_ = new BaseSimulada(filas);
        var contexto = Contexto(base_);
        return (ResolverConContexto(contexto), base_, contexto);
    }

    private static IOrderCorrelationCandidate ResolverConContexto(SalesDbContext contexto)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton(contexto);
        servicios.AddCandidateSalesOrderCorrelation();

        var proveedor = servicios.BuildServiceProvider();
        return proveedor.CreateScope().ServiceProvider.GetRequiredService<IOrderCorrelationCandidate>();
    }

    private static SalesDbContext Contexto(BaseSimulada? interceptor)
    {
        var opciones = new DbContextOptionsBuilder<SalesDbContext>().UseNpgsql(CadenaSinBase);

        if (interceptor is not null)
        {
            opciones.AddInterceptors(interceptor);
        }

        return new SalesDbContext(opciones.Options, new NodeIdentity("prueba"), TimeProvider.System);
    }

    /// <summary>
    /// El transporte sustituido: no abre ninguna conexión, apunta cada SQL y responde a
    /// las lecturas con las filas dadas, filtrando por el parámetro con igualdad
    /// <b>ordinal</b>. Cualquier escritura se cuenta y se rechaza.
    /// </summary>
    private sealed class BaseSimulada(IReadOnlyList<OrderIdentityRecord> filas)
        : DbConnectionInterceptor, IDbCommandInterceptor
    {
        public List<string> Consultas { get; } = [];

        public int Escrituras { get; private set; }

        public override InterceptionResult ConnectionOpening(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());

        public override InterceptionResult ConnectionClosing(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult> ConnectionClosingAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => ValueTask.FromResult(InterceptionResult.Suppress());

        public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(Responder(command)));

        public InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
            => InterceptionResult<DbDataReader>.SuppressWithResult(Responder(command));

        public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => throw Escritura(command);

        public InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
            => throw Escritura(command);

        private InvalidOperationException Escritura(DbCommand command)
        {
            Escrituras++;
            return new InvalidOperationException($"Escritura inesperada: {command.CommandText}");
        }

        private DbDataReader Responder(DbCommand command)
        {
            Consultas.Add(command.CommandText);

            var valor = Assert.Single(command.Parameters.Cast<DbParameter>()).Value;
            var tabla = new DataTable();
            tabla.Columns.Add("order_id", typeof(Guid));
            tabla.Columns.Add("order_code", typeof(string));

            foreach (var fila in filas.Where(f => valor switch
                     {
                         string codigo => string.Equals(f.OrderCode, codigo, StringComparison.Ordinal),
                         Guid id => f.OrderId == id,
                         _ => false,
                     }))
            {
                tabla.Rows.Add(fila.OrderId, fila.OrderCode);
            }

            return tabla.CreateDataReader();
        }
    }
}
