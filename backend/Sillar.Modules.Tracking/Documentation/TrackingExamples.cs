using Sillar.Modules.Tracking.Dtos;
using Sillar.Shared.Platform;

namespace Sillar.Modules.Tracking.Documentation;

/// <summary>Cuerpos copiables de M06 para el documento OpenAPI.</summary>
public sealed class TrackingExamples : ISchemaExamples
{
    public IReadOnlyDictionary<Type, string> Examples => Bodies;

    private static readonly Dictionary<Type, string> Bodies = new()
    {
        [typeof(SetTrackingPriorityRequest)] = """
            {
              "boardPriority": 0,
              "pinned": true,
              "orderedPeerIds": null
            }
            """,
        [typeof(SetTrackingDueRequest)] = """
            {
              "internalDueAt": "2026-10-09T16:00:00-05:00"
            }
            """,
        [typeof(AddTrackingNoteRequest)] = """
            {
              "body": "Material recibido; iniciar el acabado por la mañana."
            }
            """,
        [typeof(TransitionTrackingStatusRequest)] = """
            {
              "expectedStatus": "in_progress",
              "targetStatus": "ready"
            }
            """
    };
}
