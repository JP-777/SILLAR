using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Data;

/// <summary>
/// Ningún pago nuevo sin origen ratificado, y ningún origen reescrito.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por la opción B ratificada:</b> <c>source</c> es <c>NULL</c> solo en los pagos que
/// ya existían antes de la columna. Que esa frontera se mantenga depende de que nadie
/// escriba un <c>NULL</c> nuevo ni rellene uno viejo; aquí se impide antes de llegar a
/// la base, y la migración lo repite en SQL para quien escriba sin pasar por EF.
/// </para>
/// <para>
/// Está en el contexto y no en un escritor porque M03 todavía no tiene ninguno: el que
/// llegue pasa por aquí sin tener que acordarse.
/// </para>
/// </remarks>
internal static class OrigenDePagoObligatorio
{
    /// <exception cref="InvalidOperationException">
    /// Un pago nuevo sin origen ratificado, o un pago existente cuyo origen cambia.
    /// </exception>
    public static void Comprobar(ChangeTracker cambios)
    {
        cambios.DetectChanges();

        foreach (var pago in cambios.Entries<OrderPayment>())
        {
            if (pago.State == EntityState.Added && !PaymentSource.Ratified.Contains(pago.Entity.Source))
            {
                throw new InvalidOperationException(
                    $"Un pago nuevo exige un origen ratificado ({string.Join(", ", PaymentSource.Ratified)}); " +
                    $"llegó '{pago.Entity.Source ?? "null"}'.");
            }

            if (pago.State == EntityState.Modified && pago.Property(p => p.Source).IsModified)
            {
                throw new InvalidOperationException(
                    "El origen de un pago registrado no se cambia: un histórico sin origen no se reclasifica.");
            }
        }
    }
}
