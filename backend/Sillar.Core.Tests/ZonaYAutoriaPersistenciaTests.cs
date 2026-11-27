using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core.Auditing;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Domain;
using Sillar.Core.Dtos;
using Sillar.Core.Services;
using Sillar.Core.Settings;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

public sealed class ZonaYAutoriaPersistenciaTests
{
    [Fact]
    public async Task La_zona_y_la_auditoria_conservan_evidencia_real_y_rechazan_caminos_alternativos()
    {
        var original = Cadena() ?? throw new InvalidOperationException("Falta la conexión efímera.");
        var declared = Environment.GetEnvironmentVariable("SILLAR_VERIFY_DATABASE");
        Assert.Matches(@"^sillar_verify_\d+_\d+$", declared ?? "");
        Assert.Equal(declared, new NpgsqlConnectionStringBuilder(original).Database);
        var name = $"sillar_verify_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Random.Shared.Next(100000, int.MaxValue)}";
        var connection = new NpgsqlConnectionStringBuilder(original) { Database = name, Pooling = false }.ConnectionString;
        var maintenance = new NpgsqlConnectionStringBuilder(original) { Database = "postgres", Pooling = false }.ConnectionString;
        var ct = TestContext.Current.CancellationToken;
        await EjecutarAsync(maintenance, $"CREATE DATABASE \"{name}\"", ct);
        try
        {
            await using var db = Contexto(connection);
            // Migra una base con historia anterior: no se atribuye su correo
            // antiguo a una persona cuyo nombre actual podría ser otro.
            await db.GetService<IMigrator>().MigrateAsync("20261104203034_CoreInstallationCurrency", ct);
            await EjecutarAsync(connection, "INSERT INTO core.admin_users(full_name,email,password_hash,role,home_node) VALUES ('Nombre actual ajeno a la historia','historico@ejemplo.test','hash-no-utilizable','admin','principal'); INSERT INTO core.audit_log(action,admin_user_id,admin_user_email,summary) SELECT 'login',admin_user_id,email,'Historia anterior' FROM core.admin_users WHERE email='historico@ejemplo.test'", ct);
            await db.Database.MigrateAsync(ct);
            Assert.Null((await db.AuditLogs.SingleAsync(ct)).AdminUserName);

            var now = DateTimeOffset.UtcNow;
            db.Installations.Add(new Installation { BusinessName = "Prueba propia", InstallationKey = Guid.NewGuid(),
                ProductVersion = "1.0.0", LicenseType = "perpetual", IsSetupComplete = true });
            var actor = new AdminUser { FullName = "Nombre al actuar", Email = "actor@ejemplo.test", HomeNode = "principal",
                PasswordHash = Guid.NewGuid().ToString(), Role = AdminRole.SuperAdmin, IsActive = true };
            db.AdminUsers.Add(actor);
            await db.SaveChangesAsync(ct);
            var session = new AdminSession { AdminUserId = actor.AdminUserId, AdminSessionId = Guid.NewGuid(),
                TokenHash = Guid.NewGuid().ToString(), CsrfTokenHash = Guid.NewGuid().ToString(),
                IssuedAt = now, LastSeenAt = now, ExpiresAt = now.AddHours(8) };
            db.AdminSessions.Add(session);
            await db.SaveChangesAsync(ct);
            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, actor.AdminUserId.ToString()),
                new Claim(ClaimTypes.Email, actor.Email), new Claim(ClaimTypes.Name, actor.FullName),
                new Claim(ClaimTypes.Role, AdminRole.SuperAdmin),
                new Claim(AdminSessionClaims.HomeNode, actor.HomeNode),
                new Claim(AdminSessionClaims.SessionId, session.AdminSessionId.ToString()),
            ], "prueba"));
            var current = new CurrentAdmin(accessor);
            var writer = new AuditWriter(db, accessor, TimeProvider.System);
            using var provider = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
            var cache = new SettingsCache(provider.GetRequiredService<IServiceScopeFactory>());
            var source = new SystemTzdbSource();
            var service = new InstallationTimeZoneService(db, current, source, writer, cache, TimeProvider.System);
            var resolver = new DurableInstallationTimeZoneResolver(db, source, TimeProvider.System, NullLogger<DurableInstallationTimeZoneResolver>.Instance);

            Assert.True((await service.EstablishAsync("America/Lima", ct)).Changed);
            Assert.Equal(TimeZoneVerificationStatus.Verified, (await resolver.ResolveAsync(ct)).Status);
            Assert.Single(await db.TimeZoneAttestations.ToListAsync(ct));
            var settings = new SiteSettingService(db, cache, writer, new EventosNulos(), TimeProvider.System);
            // El segundo camino no puede modificar ni publicar la autoridad;
            // se prueba también un alias que la colación de PostgreSQL iguala.
            foreach (var key in new[] { "installation.timezone_iana", "INSTALLATION.TIMEZONE_IANA", "installation.timezone_ia\u00ADna" })
                Assert.Equal(SettingOutcome.Forbidden, (await settings.UpdateAsync(key,
                    new UpdateSettingRequest("America/Bogota", true), true, actor.AdminUserId, actor.Email, ct)).Outcome);

            // Si la auditoría falla, el ajuste y su evidencia no se confirman.
            await EjecutarAsync(connection, "CREATE FUNCTION core.rechazo_de_prueba() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Rechazo deliberado de auditoría' USING ERRCODE='23514'; END $$; CREATE TRIGGER rechazo_de_prueba BEFORE INSERT ON core.audit_log FOR EACH ROW EXECUTE FUNCTION core.rechazo_de_prueba()", ct);
            await using (var failedDb = Contexto(connection))
            {
                var failedWriter = new AuditWriter(failedDb, accessor, TimeProvider.System);
                var failedService = new InstallationTimeZoneService(failedDb, current, source, failedWriter, cache, TimeProvider.System);
                await Assert.ThrowsAsync<DbUpdateException>(() => failedService.EstablishAsync("America/Bogota", ct));
            }
            await EjecutarAsync(connection, "DROP TRIGGER rechazo_de_prueba ON core.audit_log; DROP FUNCTION core.rechazo_de_prueba()", ct);
            Assert.Equal("America/Lima", (await resolver.ResolveAsync(ct)).ZoneId);
            Assert.Single(await db.TimeZoneAttestations.AsNoTracking().ToListAsync(ct));

            // Un principal con rol antiguo no basta después de bajar el rol en la base.
            actor.Role = AdminRole.Admin;
            await db.SaveChangesAsync(ct);
            Assert.Equal(403, (await service.EstablishAsync("America/Bogota", ct)).Status);
            actor.Role = AdminRole.SuperAdmin;
            session.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            Assert.Equal(403, (await service.EstablishAsync("America/Bogota", ct)).Status);
            Assert.Single(await db.TimeZoneAttestations.ToListAsync(ct));

            actor.FullName = "Nombre posterior";
            await db.SaveChangesAsync(ct);
            var log = await db.AuditLogs.AsNoTracking().SingleAsync(x => x.EntityType == "installation_time_zone", ct);
            Assert.Equal("Nombre al actuar", log.AdminUserName);
            Assert.Equal("principal", log.AdminUserHomeNode);
            Assert.Equal(actor.AdminUserId, log.AdminUserId);
            db.AdminUsers.Remove(actor);
            await db.SaveChangesAsync(ct);
            Assert.Equal(log.AdminUserId, (await db.AuditLogs.AsNoTracking().SingleAsync(x => x.AuditLogId == log.AuditLogId, ct)).AdminUserId);
            Assert.Equal("Nombre al actuar", (await db.TimeZoneAttestations.SingleAsync(ct)).AttestedByName);

            var altered = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(connection,
                "UPDATE core.audit_log SET admin_user_name='Otro' WHERE entity_type='installation_time_zone'", ct));
            Assert.Equal(PostgresErrorCodes.CheckViolation, altered.SqlState);
            var incomplete = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(connection,
                "INSERT INTO core.audit_log(action, admin_user_name, admin_user_home_node) VALUES ('update', 'Sin id', 'principal')", ct));
            Assert.Equal(PostgresErrorCodes.CheckViolation, incomplete.SqlState);
            var changedProof = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(connection,
                "UPDATE core.installation_time_zone_attestations SET zone_id='America/Bogota'", ct));
            Assert.Equal(PostgresErrorCodes.CheckViolation, changedProof.SqlState);
            await writer.WriteAsync(new AuditEntry(AuditAction.Update) { Summary = "Acción sin actor" }, ct);
            var system = await db.AuditLogs.SingleAsync(x => x.Summary == "Acción sin actor", ct);
            Assert.Null(system.AdminUserId); Assert.Null(system.AdminUserName); Assert.Null(system.AdminUserHomeNode);
        }
        finally
        {
            // Una cancelación no cancela la retirada de la base que creó esta prueba.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await EjecutarAsync(maintenance, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", cleanup.Token);
        }
    }
}
