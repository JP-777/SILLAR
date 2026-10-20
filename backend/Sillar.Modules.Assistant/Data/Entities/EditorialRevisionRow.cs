namespace Sillar.Modules.Assistant.Data.Entities;

/// <summary>
/// Revisión editorial privada; no es fuente pública hasta que un artículo de
/// la misma instalación la señala como publicación vigente y la valida.
/// </summary>
public sealed class EditorialRevisionRow
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public Guid InstallationId { get; set; }
    public required string Question { get; set; }
    public required string Answer { get; set; }
    public required string Title { get; set; }
    public required string PublicUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    /// <summary>Versión congelada cuando se publicó; editar otro borrador no la modifica.</summary>
    public long? PublishedArticleVersion { get; set; }
}
