namespace Sillar.Modules.Cms.Dtos;

/// <summary>Sustituye el orden completo de una sección de contenido.</summary>
/// <param name="OrderedIds">Todos los identificadores, exactamente una vez, en su orden final.</param>
public sealed record ReorderCmsRequest(IReadOnlyList<int>? OrderedIds);
