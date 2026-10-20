using System.Data;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>
/// Prueba de contrato local de aislamiento. NO ejecuta SQL ni simula una carrera fisica;
/// la validez de la fotografia se verificara con PostgreSQL al abrir la puerta QA.
/// </summary>
public sealed class FaqSnapshotReadPolicyTests
{
    [Fact]
    public void RelecturaPublicaExigeFotografiaRepetible()
    {
        Assert.Equal(IsolationLevel.RepeatableRead,
            EfPublishedFaqReader.RequiredReadIsolation);
    }
}
