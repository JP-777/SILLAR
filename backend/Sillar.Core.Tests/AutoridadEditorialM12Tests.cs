using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Authentication;
using Sillar.Core.Authorization;
using Sillar.Core.Contracts;
using Sillar.Core.Domain.Values;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// M12.07 — autoridad editorial de CORE para el Asistente: deniega por defecto y
/// nunca concede.
/// </summary>
/// <remarks>
/// Lógica pura salvo el productor, cuyo contexto apunta a un puerto cerrado. Sin
/// PostgreSQL.
/// </remarks>
public sealed class AutoridadEditorialM12Tests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 26, 12, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Sesion = Guid.Parse("01920000-0000-7000-8000-0000000000c1");
    private static readonly Guid Clave = Guid.Parse("01920000-0000-7000-8000-000000000001");
    private const int Cuenta = 7;
    private static readonly EditorialAuthorityRequest Editar = new("01920000-0000-7000-8000-0000000000b1", EditorialOperation.Edit);

    private static EditorialFactsRow Hechos(
        DateTimeOffset? revocada = null,
        DateTimeOffset? vistaPorUltimaVez = null,
        bool cuentaActiva = true,
        string rol = AdminRole.Editor,
        int instalaciones = 1,
        Guid? clave = null,
        bool configurada = true,
        string? licencia = LicenseType.Subscription,
        DateTimeOffset? hasta = null,
        bool sinFecha = false,
        bool declarado = true,
        bool conActivacion = true,
        bool activo = true,
        DateTimeOffset? vence = null)
        => new(
            Sesion, Cuenta, Ahora.AddHours(-1), vistaPorUltimaVez ?? Ahora.AddMinutes(-5), revocada,
            cuentaActiva, rol, "principal",
            instalaciones, instalaciones == 0 ? null : clave ?? Clave, instalaciones == 0 ? null : configurada,
            instalaciones == 0 ? null : licencia, sinFecha || instalaciones == 0 ? null : hasta ?? Ahora.AddDays(30),
            declarado, conActivacion, activo, vence);

    private static EditorialAuthorityDecision Evaluar(EditorialFactsRow? hechos, EditorialAuthorityRequest? peticion = null, int? cuenta = Cuenta)
        => AssistantEditorialEvaluator.Evaluate(peticion ?? Editar, cuenta, hechos, Ahora);

    private static void Denegado(EditorialDenialReason razon, EditorialAuthorityDecision d)
    {
        Assert.Equal(EditorialAuthorityOutcome.Denied, d.Outcome);
        Assert.Equal(razon, d.Reason);
        Assert.False(d.IsAllowed);
        Assert.Equal(Ahora, d.ObservedAt);
    }

    // ── Todo a favor: sigue denegado ──────────────────────────────────────

    [Theory]
    [InlineData(EditorialOperation.Create, AdminRole.Editor)]
    [InlineData(EditorialOperation.Edit, AdminRole.Admin)]
    [InlineData(EditorialOperation.Publish, AdminRole.SuperAdmin)]
    [InlineData(EditorialOperation.Retire, AdminRole.SuperAdmin)]
    [InlineData(EditorialOperation.Configure, AdminRole.SuperAdmin)]
    public void Con_todos_los_hechos_a_favor_ninguna_operacion_se_concede(EditorialOperation operacion, string rol)
    {
        var d = Evaluar(Hechos(rol: rol), Editar with { Operation = operacion });

        Denegado(EditorialDenialReason.ContextualPolicyUnratified, d);
        Assert.Equal(new EditorialActor(Cuenta, "principal", rol, Sesion), d.Actor);
        Assert.Equal(Clave, d.Installation!.InstallationKey);
    }

    // ── Sesión y actor ─────────────────────────────────────────────────────

    [Fact]
    public void Una_sesion_revocada_se_deniega_aunque_los_claims_digan_lo_contrario()
    {
        var d = Evaluar(Hechos(revocada: Ahora.AddMinutes(-1)));

        Denegado(EditorialDenialReason.SessionRevoked, d);
        Assert.Null(d.Actor);
    }

    [Fact]
    public void Una_sesion_caducada_por_inactividad_se_deniega()
        => Denegado(EditorialDenialReason.SessionExpired, Evaluar(Hechos(vistaPorUltimaVez: Ahora.AddHours(-9))));

    [Fact]
    public void Una_sesion_que_no_existe_se_deniega()
        => Denegado(EditorialDenialReason.SessionUnknown, Evaluar(null));

    [Fact]
    public void Una_sesion_de_otra_cuenta_se_deniega()
        => Denegado(EditorialDenialReason.SessionUnknown, Evaluar(Hechos(), cuenta: 8));

    [Fact]
    public void Una_cuenta_desactivada_se_deniega()
        => Denegado(EditorialDenialReason.AccountInactive, Evaluar(Hechos(cuentaActiva: false)));

    [Fact]
    public void Un_rol_desconocido_se_deniega()
        => Denegado(EditorialDenialReason.RoleUnrecognized, Evaluar(Hechos(rol: "Editor")));

    // ── Instalación y licencia ─────────────────────────────────────────────

    [Fact]
    public void Sin_instalacion_ni_licencia_se_deniega()
    {
        var d = Evaluar(Hechos(instalaciones: 0));

        Denegado(EditorialDenialReason.InstallationMissing, d);
        Assert.Null(d.Installation);
    }

    [Fact]
    public void Con_dos_instalaciones_o_clave_vacia_se_deniega()
    {
        Denegado(EditorialDenialReason.InstallationIdentityInvalid, Evaluar(Hechos(instalaciones: 2)));
        Denegado(EditorialDenialReason.InstallationIdentityInvalid, Evaluar(Hechos(clave: Guid.Empty)));
    }

    [Fact]
    public void Una_instalacion_a_medio_configurar_se_deniega()
        => Denegado(EditorialDenialReason.InstallationIncomplete, Evaluar(Hechos(configurada: false)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TRIAL")]
    public void Un_tipo_de_licencia_ausente_o_desconocido_se_deniega(string? licencia)
        => Denegado(EditorialDenialReason.LicenseUnrecognized, Evaluar(Hechos(licencia: licencia)));

    [Fact]
    public void Una_licencia_temporal_sin_fecha_se_deniega()
        => Denegado(EditorialDenialReason.LicenseExpiryMissing, Evaluar(Hechos(licencia: LicenseType.Trial, sinFecha: true)));

    [Fact]
    public void Una_licencia_perpetua_con_fecha_se_deniega()
        => Denegado(EditorialDenialReason.LicenseInconsistent, Evaluar(Hechos(licencia: LicenseType.Perpetual)));

    [Fact]
    public void Una_licencia_que_vence_en_este_instante_ya_esta_vencida()
        => Denegado(EditorialDenialReason.LicenseExpired, Evaluar(Hechos(hasta: Ahora)));

    [Fact]
    public void Sin_el_modulo_assistant_declarado_activo_o_vigente_se_deniega()
    {
        Denegado(EditorialDenialReason.ModuleMissing, Evaluar(Hechos(declarado: false, conActivacion: false, activo: false)));
        Denegado(EditorialDenialReason.ModuleInactive, Evaluar(Hechos(conActivacion: false, activo: false)));
        Denegado(EditorialDenialReason.ModuleInactive, Evaluar(Hechos(activo: false)));
        Denegado(EditorialDenialReason.ModuleExpired, Evaluar(Hechos(vence: Ahora)));
    }

    // ── Entidad y operación ────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("articulo con espacios")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890")]
    public void Un_articulo_sin_identificador_valido_se_deniega(string articulo)
        => Denegado(EditorialDenialReason.ResourceInvalid, Evaluar(Hechos(), Editar with { ArticleId = articulo }));

    [Fact]
    public void Un_articulo_concreto_tampoco_se_autoriza_sin_politica()
        => Denegado(EditorialDenialReason.ContextualPolicyUnratified, Evaluar(Hechos(rol: AdminRole.SuperAdmin)));

    [Fact]
    public void Una_operacion_desconocida_se_deniega()
        => Denegado(EditorialDenialReason.OperationUnknown, Evaluar(Hechos(), Editar with { Operation = (EditorialOperation)99 }));

    // ── Productor ──────────────────────────────────────────────────────────

    private const string CadenaSinServidor = "Host=127.0.0.1;Port=1;Database=ninguna;Username=nadie;Password=nada;Timeout=2";

    private static AssistantEditorialAuthority Productor(Sillar.Core.Data.CoreDbContext contexto, bool conSesion = true)
    {
        var accesor = new HttpContextAccessor();
        if (conSesion)
        {
            accesor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Cuenta.ToString()),
                    new Claim(AdminSessionClaims.SessionId, Sesion.ToString()),
                ], "prueba")),
            };
        }

        return new AssistantEditorialAuthority(contexto, new CurrentAdmin(accesor), new RelojFijo(Ahora),
            NullLogger<AssistantEditorialAuthority>.Instance);
    }

    [Fact]
    public async Task Sin_sesion_en_la_peticion_se_deniega_sin_consultar_la_base()
    {
        await using var contexto = Contexto(CadenaSinServidor);

        Denegado(EditorialDenialReason.SessionMissing,
            await Productor(contexto, conSesion: false).EvaluateAsync(Editar, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Si_la_base_no_responde_se_deniega_por_autoridad_no_disponible()
    {
        await using var contexto = Contexto(CadenaSinServidor);

        Denegado(EditorialDenialReason.AuthorityUnavailable,
            await Productor(contexto).EvaluateAsync(Editar, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Con_el_token_del_llamador_cancelado_se_propaga_la_cancelacion()
    {
        await using var contexto = Contexto(CadenaSinServidor);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Productor(contexto).EvaluateAsync(Editar, new CancellationToken(canceled: true)));
    }

    [Fact]
    public void La_lectura_de_hechos_es_una_sola_sentencia_sobre_sesion_cuenta_instalacion_y_modulo()
    {
        using var contexto = Contexto(CadenaSinServidor);

        var sql = Productor(contexto).FactsQuery(Sesion).ToQueryString();

        Assert.Contains("core.admin_sessions", sql);
        Assert.Contains("core.admin_users", sql);
        Assert.Contains("core.installation", sql);
        Assert.Contains("core.module_activations", sql);
        Assert.DoesNotContain(";", sql);
    }

    [Fact]
    public void CORE_no_registra_el_productor_editorial_por_su_cuenta()
    {
        var servicios = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        servicios.AddCoreEssentials(Configuracion(), CadenaSinServidor).AddCoreAuthentication();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IAssistantEditorialAuthority));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora.ToUniversalTime();
    }
}
