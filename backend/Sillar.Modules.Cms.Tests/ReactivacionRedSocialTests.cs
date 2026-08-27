using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Modules.Cms.Data;
using Sillar.Modules.Cms.Dtos;
using Sillar.Modules.Cms.Endpoints;
using Sillar.Modules.Cms.Services;
using Sillar.Shared.Configuration;

namespace Sillar.Modules.Cms.Tests;

public sealed class ReactivacionRedSocialTests
{
    private sealed class AuditoriaEspia : IAuditWriter
    {
        internal List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class UsuarioPrueba : ICurrentAdmin
    {
        public int? AdminUserId => 7;
        public string? Email => "admin-prueba@sillar.test";
        public string? Role => AdminRole.Admin;
        public bool IsInRole(string role) => role == AdminRole.Editor || role == AdminRole.Admin;
    }

    [Fact]
    public async Task Reactivar_exige_editor_y_admin_para_aplicar_la_jerarquia_existente()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<SocialLinkService>();
        builder.Services.AddSingleton<IAuditWriter, AuditoriaEspia>();
        builder.Services.AddSingleton<ICurrentAdmin, UsuarioPrueba>();
        await using var app = builder.Build();
        app.MapSocialLinkEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == "/api/admin/cms/social-links/{id:int}/reactivate");
        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(data => data.Policy)
            .Where(policy => policy is not null)
            .ToArray();

        Assert.Contains(AdminRole.Editor, policies);
        Assert.Contains(AdminRole.Admin, policies);
    }

    [Fact]
    public async Task Crear_desactivar_y_reactivar_instagram_recupera_la_misma_fila()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await AbrirODescartarAsync(ct);
        if (database is null) return;
        await using var transaction = await database.Database.BeginTransactionAsync(ct);

        var previousInstagram = await database.SocialLinks
            .Where(link => link.Platform == "instagram")
            .ToListAsync(ct);
        database.SocialLinks.RemoveRange(previousInstagram);
        await database.SaveChangesAsync(ct);

        var service = new SocialLinkService(database, new CmsOrderService(database));
        var created = await service.CreateAsync(
            new CreateSocialLinkRequest("Instagram", "https://instagram.com/sillar-prueba"), ct);
        Assert.Equal(CmsOutcome.Ok, created.Outcome);
        var original = created.Value!;

        var deactivated = await service.DeactivateAsync(original.Id, ct);
        Assert.Equal(CmsOutcome.Ok, deactivated.Outcome);
        Assert.False(deactivated.Value!.IsActive);
        Assert.DoesNotContain(await service.ListPublicAsync(ct), link => link.Id == original.Id);

        var audit = new AuditoriaEspia();
        await SocialLinkEndpoints.Reactivate(original.Id, service, audit, new UsuarioPrueba(), ct);

        var reactivated = await service.GetAsync(original.Id, ct);
        Assert.NotNull(reactivated);
        Assert.True(reactivated.IsActive);
        Assert.Equal(original.Id, reactivated.Id);
        Assert.Equal(original.Platform, reactivated.Platform);
        Assert.Equal(original.Url, reactivated.Url);
        Assert.Equal(original.DisplayOrder, reactivated.DisplayOrder);
        Assert.Contains(await service.ListPublicAsync(ct), link => link.Id == original.Id);

        var duplicate = await service.CreateAsync(
            new CreateSocialLinkRequest("instagram", "https://instagram.com/otra-cuenta"), ct);
        Assert.Equal(CmsOutcome.Conflict, duplicate.Outcome);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditAction.Activate, entry.Action);
        Assert.Equal("cms", entry.ModuleCode);
        Assert.Equal("social_link", entry.EntityType);
        Assert.Equal(original.Id.ToString(), entry.EntityId);

        await transaction.RollbackAsync(ct);
    }

    [Fact]
    public async Task Reactivar_una_activa_es_idempotente_y_un_id_inexistente_no_se_inventa()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await AbrirODescartarAsync(ct);
        if (database is null) return;
        await using var transaction = await database.Database.BeginTransactionAsync(ct);

        var previousYoutube = await database.SocialLinks
            .Where(link => link.Platform == "youtube")
            .ToListAsync(ct);
        database.SocialLinks.RemoveRange(previousYoutube);
        await database.SaveChangesAsync(ct);

        var service = new SocialLinkService(database, new CmsOrderService(database));
        var created = await service.CreateAsync(
            new CreateSocialLinkRequest("youtube", "https://youtube.com/@sillar-prueba"), ct);
        var before = created.Value!;

        var again = await service.ReactivateAsync(before.Id, ct);
        Assert.Equal(CmsOutcome.Ok, again.Outcome);
        Assert.Equal(before, again.Value);

        var audit = new AuditoriaEspia();
        var missing = await SocialLinkEndpoints.Reactivate(
            int.MaxValue,
            service,
            audit,
            new UsuarioPrueba(),
            ct);
        await using var httpServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = httpServices };
        await missing.ExecuteAsync(http);
        Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode);
        Assert.Empty(audit.Entries);

        await transaction.RollbackAsync(ct);
    }

    private static async Task<CmsDbContext?> AbrirODescartarAsync(CancellationToken ct)
    {
        DotEnv.Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Las pruebas de persistencia CMS son destructivas y solo pueden ejecutarse " +
                "contra la base efímera creada por scripts/verificar.mjs. " +
                "Falta ConnectionStrings__Default en el entorno.");
        }

        // La única autoridad para el nombre de la base es la puerta
        // scripts/verificar.mjs, que pasa SILLAR_VERIFY_DATABASE al proceso
        // backend. Estas pruebas escriben (crean/desactivan/reactivan redes
        // sociales) y solo corren contra esa base efímera. Se comprueba
        // coincidencia exacta —no un prefijo— para que la regla se defina
        // una sola vez, aquí y en CRM. Si no coincide, falla inmediatamente.
        var verifyDb = Environment.GetEnvironmentVariable("SILLAR_VERIFY_DATABASE");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(verifyDb))
        {
            throw new InvalidOperationException(
                "Las pruebas de persistencia CMS son destructivas y solo pueden ejecutarse " +
                "contra la base efímera creada por scripts/verificar.mjs. " +
                "Falta SILLAR_VERIFY_DATABASE en el entorno.");
        }
        if (!string.Equals(builder.Database, verifyDb, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Las pruebas de persistencia CMS son destructivas y solo pueden ejecutarse " +
                "contra la base efímera creada por scripts/verificar.mjs. " +
                $"SILLAR_VERIFY_DATABASE='{verifyDb}' pero la conexión apunta a '{builder.Database}'.");
        }

        var options = new DbContextOptionsBuilder<CmsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var database = new CmsDbContext(options);
        if (!await database.Database.CanConnectAsync(ct)
            || !await ExisteSchemaCmsAsync(database, ct))
        {
            await database.DisposeAsync();
            throw new InvalidOperationException(
                "La base efímera no responde o no tiene aplicada la migración de CMS.");
        }

        return database;
    }

    private static async Task<bool> ExisteSchemaCmsAsync(CmsDbContext database, CancellationToken ct)
    {
        await database.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT to_regclass('cms.social_links') IS NOT NULL";
            return await command.ExecuteScalarAsync(ct) is true;
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }
}
