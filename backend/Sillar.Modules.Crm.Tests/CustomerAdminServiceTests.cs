using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Core.Contracts.Email;
using Sillar.Modules.Crm.Administration;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Domain;
using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Tests;

[Collection("CrmDb")]
public sealed class CustomerAdminServiceTests(
    CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Crea_ficha_sin_cuenta_y_la_encuentra_por_busqueda()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = fixture.CreateContext();
        var harness = Harness(db);

        var created = await harness.Service.CreateAsync(
            new CreateAdminCustomerRequest(
                "María Quispe",
                "maria.quispe@ejemplo.pe",
                "+51 900 000 001",
                "dni",
                "11223344",
                "Prefiere contacto por la tarde."),
            7,
            "admin@ejemplo.pe",
            TestContext.Current.CancellationToken);

        Assert.Equal(CustomerAdminOutcome.Ok, created.Outcome);
        Assert.NotNull(created.Customer);
        Assert.Equal("no_account", created.Customer.Access.State);
        Assert.Equal(
            "Prefiere contacto por la tarde.",
            created.Customer.InternalNotes);

        var accounts = await db.CustomerAccounts
            .AsNoTracking()
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, accounts);

        var list = await harness.Service.ListAsync(
            "11223344",
            TestContext.Current.CancellationToken);

        var found = Assert.Single(list);
        Assert.Equal(created.Customer.CustomerId, found.CustomerId);
        Assert.Equal("María Quispe", found.FullName);
        Assert.DoesNotContain(
            harness.Audit.Entries,
            entry => entry.Summary?.Contains(
                "Prefiere contacto",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Invitacion_marca_estado_y_no_expone_el_token()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = fixture.CreateContext();
        var harness = Harness(db);

        var created = await harness.Service.CreateAsync(
            new CreateAdminCustomerRequest(
                "Cliente Invitado",
                "invitado.admin@ejemplo.pe",
                null,
                null,
                null,
                null),
            7,
            "admin@ejemplo.pe",
            TestContext.Current.CancellationToken);

        var customerId = created.Customer!.CustomerId;

        var invited = await harness.Service.InviteAsync(
            customerId,
            "https://tienda.ejemplo.test",
            7,
            "admin@ejemplo.pe",
            TestContext.Current.CancellationToken);

        Assert.Equal(CustomerAdminOutcome.Ok, invited.Outcome);
        Assert.NotNull(invited.Invitation);
        Assert.True(invited.Invitation.EmailSent);
        Assert.Equal("invited", invited.Customer!.Access.State);
        Assert.Single(harness.Email.Messages);

        var token = await db.CustomerTokens
            .AsNoTracking()
            .SingleAsync(
                stored =>
                    stored.CustomerId == customerId
                    && stored.Purpose == CustomerTokenPurpose.Invitation,
                TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            token.TokenHash,
            harness.Email.Messages[0].TextBody,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            harness.Audit.Entries,
            entry => entry.Summary?.Contains(
                token.TokenHash,
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Baja_revoca_sesiones_y_reactivar_no_las_resucita()
    {
        await fixture.CleanAllTablesAsync();

        Guid customerId;
        int accountId;

        await using (var seed = fixture.CreateContext())
        {
            var customer = new Customer
            {
                FullName = "Cliente Con Sesión",
                Email = "sesion.admin@ejemplo.pe",
                IsActive = true
            };

            seed.Customers.Add(customer);
            await seed.SaveChangesAsync(
                TestContext.Current.CancellationToken);

            var account = new CustomerAccount
            {
                CustomerId = customer.CustomerId,
                PasswordHash = "hash-no-secreto-de-test"
            };

            seed.CustomerAccounts.Add(account);
            await seed.SaveChangesAsync(
                TestContext.Current.CancellationToken);

            customerId = customer.CustomerId;
            accountId = account.CustomerAccountId;

            var sessions = new CustomerSessionService(
                seed,
                new FixedTimeProvider(Now));

            await sessions.OpenAsync(
                accountId,
                Now.AddHours(2),
                "127.0.0.1",
                "test",
                TestContext.Current.CancellationToken);
        }

        await using var db = fixture.CreateContext();
        var harness = Harness(db);

        var deactivated = await harness.Service.DeactivateAsync(
            customerId,
            7,
            "admin@ejemplo.pe",
            TestContext.Current.CancellationToken);

        Assert.Equal(CustomerAdminOutcome.Ok, deactivated.Outcome);
        Assert.Equal("deactivated", deactivated.Customer!.Access.State);

        db.ChangeTracker.Clear();

        var storedSession = await db.CustomerSessions
            .AsNoTracking()
            .SingleAsync(
                session => session.CustomerAccountId == accountId,
                TestContext.Current.CancellationToken);

        Assert.Equal(Now, storedSession.RevokedAt);

        var reactivated = await harness.Service.ReactivateAsync(
            customerId,
            7,
            "admin@ejemplo.pe",
            TestContext.Current.CancellationToken);

        Assert.Equal(CustomerAdminOutcome.Ok, reactivated.Outcome);
        Assert.Equal("active", reactivated.Customer!.Access.State);

        db.ChangeTracker.Clear();

        storedSession = await db.CustomerSessions
            .AsNoTracking()
            .SingleAsync(
                session => session.CustomerAccountId == accountId,
                TestContext.Current.CancellationToken);

        Assert.Equal(Now, storedSession.RevokedAt);
    }

    [Fact]
    public async Task Editar_correo_de_cuenta_verificada_la_deja_sin_verificar()
    {
        await fixture.CleanAllTablesAsync();

        Guid customerId;

        await using (var seed = fixture.CreateContext())
        {
            var customer = new Customer
            {
                FullName = "Cliente Verificado",
                Email = "antes.admin@ejemplo.pe",
                IsActive = true
            };

            seed.Customers.Add(customer);
            await seed.SaveChangesAsync(
                TestContext.Current.CancellationToken);

            seed.CustomerAccounts.Add(
                new CustomerAccount
                {
                    CustomerId = customer.CustomerId,
                    PasswordHash = "hash-no-secreto-de-test",
                    EmailVerifiedAt = Now
                });

            await seed.SaveChangesAsync(
                TestContext.Current.CancellationToken);

            customerId = customer.CustomerId;
        }

        await using var db = fixture.CreateContext();
        var harness = Harness(db);

        var updated = await harness.Service.UpdateAsync(
            customerId,
            new UpdateAdminCustomerRequest(
                "Cliente Verificado",
                "despues.admin@ejemplo.pe",
                null,
                null,
                null,
                "Nota interna permitida en panel."),
            7,
            "admin@ejemplo.pe",
            TestContext.Current.CancellationToken);

        Assert.Equal(CustomerAdminOutcome.Ok, updated.Outcome);
        Assert.False(updated.Customer!.Access.EmailVerified);
        Assert.Equal(
            "Nota interna permitida en panel.",
            updated.Customer.InternalNotes);
    }

    private HarnessResult Harness(
        Sillar.Modules.Crm.Data.CrmDbContext db)
    {
        var clock = new FixedTimeProvider(Now);
        var audit = new RecordingAuditWriter();
        var email = new RecordingEmailSender();
        var tokens = new CustomerAccountTokenService(
            db,
            new CustomerPasswordHasher(),
            clock);

        var service = new CustomerAdminService(
            db,
            tokens,
            email,
            audit,
            clock);

        return new HarnessResult(
            service,
            audit,
            email);
    }

    private sealed record HarnessResult(
        CustomerAdminService Service,
        RecordingAuditWriter Audit,
        RecordingEmailSender Email);

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

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<OutgoingEmail> Messages { get; } = [];

        public Task<EmailSendResult> SendAsync(
            OutgoingEmail message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(
                new EmailSendResult(true));
        }
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
