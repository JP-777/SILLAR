namespace Sillar.Modules.Assistant.Data.Entities;

/// <summary>
/// Raíz de artículo. Punteros a revisión no son autoridad por sí solos:
/// la publicación física futura exige una transacción y verificación de pertenencia.
/// </summary>
public sealed class EditorialArticleRow
{
    public Guid Id { get; set; }
    public Guid InstallationId { get; set; }
    public required string WriterNode { get; set; }
    public long Version { get; set; }
    public Guid? CurrentDraftRevisionId { get; set; }
    public Guid? CurrentPublishedRevisionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
