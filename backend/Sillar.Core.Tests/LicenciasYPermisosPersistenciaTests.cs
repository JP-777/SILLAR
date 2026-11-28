using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core.Auditing;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Domain;
using Sillar.Core.Modularity;
using Sillar.Core.Services;
using Sillar.Shared.Modularity;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

public sealed class LicenciasYPermisosPersistenciaTests
{
    [Fact]
    public async Task La_licencia_precede_a_la_activacion_y_el_permiso_se_relee_en_la_operacion()
    {
        var original = Cadena() ?? throw new InvalidOperationException("Falta la conexión efímera.");
        var declaredDatabase = Environment.GetEnvironmentVariable("SILLAR_VERIFY_DATABASE");
        Assert.Matches(@"^sillar_verify_\d+_\d+$", declaredDatabase ?? "");
        Assert.Equal(declaredDatabase, new NpgsqlConnectionStringBuilder(original).Database);
        var name = $"sillar_verify_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Random.Shared.Next(100000, int.MaxValue)}";
        var connection = new NpgsqlConnectionStringBuilder(original) { Database = name, Pooling = false }.ConnectionString;
        var maintenance = new NpgsqlConnectionStringBuilder(original) { Database = "postgres", Pooling = false }.ConnectionString;
        var ct = TestContext.Current.CancellationToken;
        await EjecutarAsync(maintenance, $"CREATE DATABASE \"{name}\"", ct);
        try
        {
            await using var db = Contexto(connection);
            await db.Database.MigrateAsync(ct);
            var modules = new DeclaredModules([new CoreModule(), new ModuloConOperaciones()]);
            var sync = new ModuleSynchronizer(db, NullLogger<ModuleSynchronizer>.Instance);
            Assert.Equal(["core"], (await sync.SynchronizeAsync(modules.Modules, ct)).Active.Select(m => m.Code));
            var activation = new ModuleActivationService(db, modules, new AuditoriaNula(), new EventosNulos(), TimeProvider.System, Reiniciador(), new AutoridadDeActivacionFalsa());
            var card = (await activation.ListAsync(ct)).Single(m => m.Code == "prueba_permisos");
            Assert.False(card.IsLicensed); Assert.False(card.CanActivate); Assert.False(card.IsActive);
            Assert.Equal(ActivationOutcome.NotFound, (await activation.SetActiveAsync("prueba_permisos", true, ct)).Outcome);

            var now = DateTimeOffset.UtcNow;
            var actor = new AdminUser { FullName = "Persona al actuar", Email = "actor@ejemplo.test", HomeNode = "principal",
                PasswordHash = Guid.NewGuid().ToString(), Role = AdminRole.SuperAdmin, IsActive = true };
            db.AdminUsers.Add(actor); await db.SaveChangesAsync(ct);
            var session = new AdminSession { AdminUserId = actor.AdminUserId, AdminSessionId = Guid.NewGuid(),
                TokenHash = Guid.NewGuid().ToString(), CsrfTokenHash = Guid.NewGuid().ToString(),
                IssuedAt = now, LastSeenAt = now, ExpiresAt = now.AddHours(8) };
            db.AdminSessions.Add(session); await db.SaveChangesAsync(ct);
            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, actor.AdminUserId.ToString()), new Claim(ClaimTypes.Email, actor.Email),
                new Claim(ClaimTypes.Name, actor.FullName), new Claim(ClaimTypes.Role, AdminRole.SuperAdmin),
                new Claim(AdminSessionClaims.HomeNode, actor.HomeNode), new Claim(AdminSessionClaims.SessionId, session.AdminSessionId.ToString())
            ], "prueba"));
            var authority = new AdminSessionAuthority(db, new CurrentAdmin(accessor), TimeProvider.System);
            activation = new ModuleActivationService(db, modules, new AuditoriaNula(), new EventosNulos(), TimeProvider.System, Reiniciador(), authority);
            var authorization = new ModuleAuthorization(db, modules, new RegistroActivo(), authority);
            async Task Denied(int status, string operation = "read") => Assert.Equal(status,
                (await Assert.ThrowsAsync<ModuleAccessDeniedException>(() => authorization.RequireAsync("prueba_permisos", operation, ct))).StatusCode);
            await Denied(404); // Ni un registro de arranque optimista puede saltarse la licencia.
            await ModuleLicensing.ApplyProvisioningAsync(db, modules.Modules, LicenciasDePrueba(modules.Modules), ct);
            await Denied(404); // Tener licencia no activa.
            Assert.Equal(ActivationOutcome.Changed, (await activation.SetActiveAsync("prueba_permisos", true, ct)).Outcome);
            Assert.Equal(actor.AdminUserId, (await authorization.RequireAsync("prueba_permisos", "read", ct)).LocalId);
            var writer = new AuditWriter(db, accessor, TimeProvider.System);
            var permissions = new ModulePermissionService(db, modules, authority, writer, TimeProvider.System);
            await permissions.SetAsync(actor.AdminUserId, "prueba_permisos", "read", false, ct);
            await Denied(403);
            Assert.Equal(actor.AdminUserId, (await authorization.RequireAsync("prueba_permisos", "export", ct)).LocalId);
            await permissions.SetAsync(actor.AdminUserId, "prueba_permisos", "read", null, ct);
            Assert.Empty(await db.ModuleOperationPermissions.AsNoTracking().ToListAsync(ct));
            await authorization.RequireAsync("prueba_permisos", "read", ct);

            // Si falla la auditoría, no queda una denegación que nadie pueda explicar.
            await EjecutarAsync(connection, "CREATE FUNCTION core.rechazo_permiso_prueba() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Rechazo deliberado de auditoría' USING ERRCODE='23514'; END $$; CREATE TRIGGER rechazo_permiso_prueba BEFORE INSERT ON core.audit_log FOR EACH ROW EXECUTE FUNCTION core.rechazo_permiso_prueba()", ct);
            await using (var failed = Contexto(connection))
            {
                var failedAuthority = new AdminSessionAuthority(failed, new CurrentAdmin(accessor), TimeProvider.System);
                var failedService = new ModulePermissionService(failed, modules, failedAuthority, new AuditWriter(failed, accessor, TimeProvider.System), TimeProvider.System);
                await Assert.ThrowsAsync<DbUpdateException>(() => failedService.SetAsync(actor.AdminUserId, "prueba_permisos", "read", false, ct));
            }
            await EjecutarAsync(connection, "DROP TRIGGER rechazo_permiso_prueba ON core.audit_log; DROP FUNCTION core.rechazo_permiso_prueba()", ct);
            Assert.Empty(await db.ModuleOperationPermissions.AsNoTracking().ToListAsync(ct));
            await permissions.SetAsync(actor.AdminUserId, "prueba_permisos", "read", true, ct);
            actor.Role = AdminRole.Editor; await db.SaveChangesAsync(ct);
            Assert.Equal(403, (await Assert.ThrowsAsync<ModuleAccessDeniedException>(() => activation.SetActiveAsync("prueba_permisos", false, ct))).StatusCode);
            await Denied(403); // El permiso explícito no supera el rol real ni cree el claim antiguo.
            await Assert.ThrowsAsync<ModuleAccessDeniedException>(() => permissions.SetAsync(actor.AdminUserId, "prueba_permisos", "read", null, ct));
            actor.Role = AdminRole.SuperAdmin; actor.FullName = "Nombre cambiado"; await db.SaveChangesAsync(ct);
            Assert.All(await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "module_permission").ToListAsync(ct), log => Assert.Equal("Persona al actuar", log.AdminUserName));
            Assert.Equal("Nombre cambiado", (await authorization.RequireAsync("prueba_permisos", "read", ct)).DisplayName);
            session.RevokedAt = now; await db.SaveChangesAsync(ct);
            await Denied(401);
            session.RevokedAt = null; await db.SaveChangesAsync(ct);
            var onlyCore = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [ModuleLicensing.ConfigurationKey] = "core" }).Build();
            await ModuleLicensing.ApplyProvisioningAsync(db, modules.Modules, onlyCore, ct);
            await Denied(404); // La retirada se ve antes del próximo reinicio.
            Assert.Equal(["core"], (await sync.SynchronizeAsync(modules.Modules, ct)).Active.Select(m => m.Code));
            Assert.Equal(ActivationOutcome.NotFound, (await activation.SetActiveAsync("prueba_permisos", true, ct)).Outcome);
            Assert.All(await db.AuditLogs.Where(a => a.EntityType == "module_license").ToListAsync(ct), log => {
                Assert.Null(log.AdminUserId); Assert.Null(log.AdminUserName); Assert.Null(log.AdminUserHomeNode);
            });
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await EjecutarAsync(maintenance, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", cleanup.Token);
        }
    }

    [Fact]
    public void Provisionar_no_inventa_modulos_ni_concede_dependencias_silenciosamente()
    {
        IModule[] modules = [new CoreModule(), new ModuloConOperaciones()];
        Assert.Null(ModuleLicensing.Parse("", modules));
        Assert.Throws<InvalidOperationException>(() => ModuleLicensing.Parse("ausente", modules));
        Assert.Throws<InvalidOperationException>(() => ModuleLicensing.Parse("prueba_permisos", [new CoreModule(), new ModuloConOperaciones(["core", "dependencia"]), new ModuloDependencia()]));
        Assert.Equal(["core"], ModuleLicensing.Parse("core", modules));
    }

    [Theory]
    [InlineData("Read", "admin")]
    [InlineData("read", "dueño")]
    [InlineData("read-external", "admin")]
    public void El_catalogo_rechaza_una_operacion_invalida_antes_de_arrancar(string code, string role)
    {
        var malformed = new ModuloConOperaciones { Operations = [new(code, "Consultar", role)] };
        Assert.False(ModuleGraph.Validate([new CoreModule(), malformed]).IsValid);
        var duplicate = new ModuloConOperaciones { Operations = [new("read", "Consultar", "admin"), new("read", "Repetida", "admin")] };
        Assert.False(ModuleGraph.Validate([new CoreModule(), duplicate]).IsValid);
        Assert.True(ModuleGraph.Validate([new CoreModule(), new ModuloConOperaciones()]).IsValid);
    }

    private sealed class RegistroActivo : IModuleRegistry
    {
        public bool IsActive(string code) => true;
        public IReadOnlyList<ActiveModule> GetActive() => [];
    }
    private sealed class ModuloConOperaciones(string[]? dependencies = null) : IModule
    {
        public string Code => "prueba_permisos"; public string? Schema => null;
        public string DisplayName => "Módulo de prueba"; public string Description => "Escenario físico de autoridad.";
        public string Version => "1.0.0"; public int DisplayOrder => 1;
        public string[] HardDependencies => dependencies ?? ["core"]; public string[] SoftDependencies => [];
        public IReadOnlyList<ModuleOperation> Operations { get; init; } = [new("read", "Consultar", "admin"), new("export", "Exportar", "admin")];
        public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }
        public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
    }
    private sealed class ModuloDependencia : IModule
    {
        public string Code => "dependencia"; public string? Schema => null;
        public string DisplayName => "Dependencia"; public string Description => "Dependencia explícita de prueba.";
        public string Version => "1.0.0"; public int DisplayOrder => 2;
        public string[] HardDependencies => ["core"]; public string[] SoftDependencies => [];
        public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }
        public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
    }
}
