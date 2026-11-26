using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Audit;
using Sillar.Modules.Assistant.Authorization;
using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.CoreComposition.Tests;

public sealed class ProyeccionAuditoriaEditorialTests
{
    private static readonly Guid Article = Guid.Parse("01900000-0000-7000-8000-000000000001");

    [Theory]
    [InlineData(AssistantEditorialPermission.Publish, EditorialResult.Published, "publish:published")]
    [InlineData(AssistantEditorialPermission.Withdraw, EditorialResult.Withdrawn, "withdraw:withdrawn")]
    [InlineData(AssistantEditorialPermission.Publish, EditorialResult.Forbidden, "publish:forbidden")]
    [InlineData(AssistantEditorialPermission.Withdraw, EditorialResult.Conflict, "withdraw:conflict")]
    [InlineData(AssistantEditorialPermission.Publish, EditorialResult.Invalid, "publish:invalid")]
    [InlineData(AssistantEditorialPermission.Withdraw, EditorialResult.Unchanged, "withdraw:unchanged")]
    public void Conserva_operacion_resultado_y_recurso_sin_contenido_ni_correo(
        AssistantEditorialPermission operation, EditorialResult result, string suffix)
    {
        var entry = Assert.IsType<AuditEntry>(EditorialAuditProjection.Create(operation, result, Article, 7));
        Assert.Equal(AuditAction.Update, entry.Action);
        Assert.Equal(7, entry.AdminUserId);
        Assert.Equal("assistant", entry.ModuleCode);
        Assert.Equal("knowledge_article", entry.EntityType);
        Assert.Equal(Article.ToString("D"), entry.EntityId);
        Assert.Equal("assistant.editorial." + suffix, entry.Summary);
        Assert.Null(entry.AdminUserEmail);
    }

    [Theory]
    [InlineData(AssistantEditorialPermission.Publish, EditorialResult.Withdrawn)]
    [InlineData(AssistantEditorialPermission.Withdraw, EditorialResult.Published)]
    [InlineData(AssistantEditorialPermission.Publish, EditorialResult.Saved)]
    [InlineData(AssistantEditorialPermission.Create, EditorialResult.Forbidden)]
    [InlineData((AssistantEditorialPermission)999, EditorialResult.Forbidden)]
    [InlineData(AssistantEditorialPermission.Publish, (EditorialResult)999)]
    public void No_fabrica_evidencia_ante_operacion_o_resultado_incoherente(
        AssistantEditorialPermission operation, EditorialResult result)
        => Assert.Null(EditorialAuditProjection.Create(operation, result, Article, 7));

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void No_atribuye_exito_sin_actor_valido(int? actor)
        => Assert.Null(EditorialAuditProjection.Create(
            AssistantEditorialPermission.Publish, EditorialResult.Published, Article, actor));

    [Fact]
    public void Rechazo_sin_identidad_no_inventa_actor()
    {
        var entry = Assert.IsType<AuditEntry>(EditorialAuditProjection.Create(
            AssistantEditorialPermission.Withdraw, EditorialResult.Forbidden, Article, null));
        Assert.Null(entry.AdminUserId);
        Assert.Null(entry.AdminUserEmail);
        Assert.Equal("assistant.editorial.withdraw:forbidden", entry.Summary);
    }

    [Fact]
    public void Recurso_vacio_no_produce_evidencia()
        => Assert.Null(EditorialAuditProjection.Create(
            AssistantEditorialPermission.Publish, EditorialResult.Forbidden, Guid.Empty, 7));
}
