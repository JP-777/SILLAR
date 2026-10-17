namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Discriminante cerrado del origen de un pago: <c>staff</c> o
/// <c>provider</c>.
/// </summary>
/// <remarks>
/// Las variantes tienen formas distintas para que una atribución humana parcial,
/// una persona ficticia en un cobro automático o evidencia externa en un cobro
/// manual no sean estados representables.
/// </remarks>
public abstract record PaymentSource
{
    private PaymentSource(string code)
    {
        Code = code;
    }

    public string Code { get; }

    /// <summary>Origen humano con sus tres datos congelados.</summary>
    public sealed record Staff : PaymentSource
    {
        public Staff(StaffPaymentAttribution attribution)
            : base(Codes.Staff)
        {
            Attribution = attribution
                ?? throw new ArgumentNullException(nameof(attribution));
        }

        public StaffPaymentAttribution Attribution { get; }
    }

    /// <summary>Origen automático sin campos humanos y con evidencia verificada.</summary>
    public sealed record Provider : PaymentSource
    {
        public Provider(VerifiedExternalPaymentEvidence evidence)
            : base(Codes.Provider)
        {
            Evidence = evidence
                ?? throw new ArgumentNullException(nameof(evidence));
        }

        public VerifiedExternalPaymentEvidence Evidence { get; }
    }

    /// <summary>Únicos códigos de origen admitidos.</summary>
    public static class Codes
    {
        public const string Staff = "staff";
        public const string Provider = "provider";
    }
}
