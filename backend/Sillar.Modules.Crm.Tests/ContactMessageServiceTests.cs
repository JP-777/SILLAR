using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Contact;
using Sillar.Modules.Crm.Domain;
using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Tests;

[Collection("CrmDb")]
public sealed class ContactMessageServiceTests(
    CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    [Fact]
    public async Task Visitante_guarda_mensaje_sin_customer_id()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = fixture.CreateContext();
        var harness = Harness(db);

        var result = await harness.Service.SubmitAsync(
            new PublicContactRequest(
                "Visitante",
                "visita@ejemplo.pe",
                null,
                "Consulta",
                "Quisiera recibir información."),
            null,
            "203.0.113.10",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ContactMessageOutcome.Ok,
            result.Outcome);

        var stored = await db.ContactMessages
            .AsNoTracking()
            .SingleAsync(
                TestContext.Current.CancellationToken);

        Assert.Null(stored.CustomerId);
        Assert.Equal(
            "Visitante",
            stored.FullName);
        Assert.Equal(
            "visita@ejemplo.pe",
            stored.Email);
    }

    [Fact]
    public async Task Sesion_valida_puede_vincular_customer_y_conserva_snapshot()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = fixture.CreateContext();

        var customer = new Customer
        {
            FullName = "Nombre De Ficha",
            Email = "ficha@ejemplo.pe",
            IsActive = true
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);

        var harness = Harness(db);

        var result = await harness.Service.SubmitAsync(
            new PublicContactRequest(
                "Nombre Escrito En Formulario",
                "contacto.distinto@ejemplo.pe",
                "+51 900 000 100",
                null,
                "Mensaje vinculado."),
            customer.CustomerId,
            "203.0.113.11",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ContactMessageOutcome.Ok,
            result.Outcome);

        db.ChangeTracker.Clear();

        var stored = await db.ContactMessages
            .AsNoTracking()
            .SingleAsync(
                TestContext.Current.CancellationToken);

        Assert.Equal(
            customer.CustomerId,
            stored.CustomerId);
        Assert.Equal(
            "Nombre Escrito En Formulario",
            stored.FullName);
        Assert.Equal(
            "contacto.distinto@ejemplo.pe",
            stored.Email);
    }

    [Fact]
    public async Task Sin_correo_ni_telefono_se_rechaza()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = fixture.CreateContext();
        var harness = Harness(db);

        var result = await harness.Service.SubmitAsync(
            new PublicContactRequest(
                "Visitante",
                null,
                null,
                null,
                "Tengo una consulta."),
            null,
            "203.0.113.12",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ContactMessageOutcome.Invalid,
            result.Outcome);

        Assert.Equal(
            0,
            await db.ContactMessages.CountAsync(
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Admin_lee_y_da_de_baja_sin_borrar()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = fixture.CreateContext();
        var harness = Harness(db);

        var submitted = await harness.Service.SubmitAsync(
            new PublicContactRequest(
                "Visitante",
                null,
                "+51 900 000 101",
                "Pedido de llamada",
                "Por favor llámenme."),
            null,
            "203.0.113.13",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ContactMessageOutcome.Ok,
            submitted.Outcome);

        var list = await harness.Service.ListAdminAsync(
            false,
            TestContext.Current.CancellationToken);

        var item = Assert.Single(list);

        var detail = await harness.Service.GetAdminAsync(
            item.ContactMessageId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(detail);
        Assert.Equal(
            "Por favor llámenme.",
            detail.Message);

        var deactivated =
            await harness.Service.DeactivateAsync(
                item.ContactMessageId,
                7,
                "admin@ejemplo.pe",
                TestContext.Current.CancellationToken);

        Assert.Equal(
            ContactMessageOutcome.Ok,
            deactivated.Outcome);
        Assert.False(
            deactivated.Contact!.IsActive);

        db.ChangeTracker.Clear();

        var stored = await db.ContactMessages
            .AsNoTracking()
            .SingleAsync(
                TestContext.Current.CancellationToken);

        Assert.False(stored.IsActive);
        Assert.Single(harness.Audit.Entries);
    }

    private static HarnessResult Harness(
        Sillar.Modules.Crm.Data.CrmDbContext db)
    {
        var throttle =
            new ContactSubmissionThrottle(
                TimeProvider.System);

        var audit = new RecordingAuditWriter();

        return new HarnessResult(
            new ContactMessageService(
                db,
                throttle,
                audit),
            audit);
    }

    private sealed record HarnessResult(
        ContactMessageService Service,
        RecordingAuditWriter Audit);

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(
            AuditEntry entry,
            CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
