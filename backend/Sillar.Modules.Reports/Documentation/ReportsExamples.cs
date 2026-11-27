using Sillar.Modules.Reports.Endpoints;
using Sillar.Shared.Platform;

namespace Sillar.Modules.Reports.Documentation;

/// <summary>Cuerpos de ejemplo de las operaciones de Reportes.</summary>
public sealed class ReportsExamples : ISchemaExamples
{
    public IReadOnlyDictionary<Type, string> Examples { get; } = new Dictionary<Type, string>
    {
        [typeof(CsvExportBody)] = """
            { "startInclusive": "2026-11-01T00:00:00-05:00", "endExclusive": "2026-12-01T00:00:00-05:00", "zone": "America/Lima" }
            """,
    };
}
