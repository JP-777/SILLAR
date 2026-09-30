using Microsoft.EntityFrameworkCore;
using Sillar.Modules.B2B.Domain;

namespace Sillar.Modules.B2B.Data;

/// <summary>Contexto de datos de M07. Solo escribe en el schema <c>b2b</c>.</summary>
public sealed class B2bDbContext : DbContext
{
    public const string Schema = "b2b";
    public const string MigrationsHistoryTable = "__migrations";

    public B2bDbContext(DbContextOptions<B2bDbContext> options) : base(options)
    {
    }

    public DbSet<SpecialOrderLead> SpecialOrderLeads => Set<SpecialOrderLead>();
    public DbSet<InstitutionRequest> InstitutionRequests => Set<InstitutionRequest>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<QuoteNumberSeries> QuoteNumberSeries => Set<QuoteNumberSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(B2bDbContext).Assembly);
    }
}
