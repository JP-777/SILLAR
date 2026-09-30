using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.B2B.Domain;

namespace Sillar.Modules.B2B.Data.Configurations;

internal static class Reglas
{
    internal static string NoVacio(string columna) => $"btrim({columna}) <> ''";
    internal static string OpcionalNoVacio(string columna) => $"{columna} IS NULL OR btrim({columna}) <> ''";

    internal const string EstadosDeSolicitud =
        "status IN ('recibida', 'en_revision', 'cotizada', 'cerrada', 'rechazada')";

    internal static void Tiempos<T>(EntityTypeBuilder<T> b) where T : class
    {
        b.Property<bool>("IsActive").HasColumnName("is_active").HasDefaultValue(true);
        b.Property<DateTimeOffset>("CreatedAt").HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        b.Property<DateTimeOffset>("UpdatedAt").HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
    }
}

internal sealed class SpecialOrderLeadConfiguration : IEntityTypeConfiguration<SpecialOrderLead>
{
    public void Configure(EntityTypeBuilder<SpecialOrderLead> b)
    {
        b.ToTable("special_order_leads", t =>
        {
            t.HasCheckConstraint("ck_special_order_leads_description", Reglas.NoVacio("description"));
            t.HasCheckConstraint("ck_special_order_leads_quantity", "quantity IS NULL OR quantity > 0");
            t.HasCheckConstraint("ck_special_order_leads_product_name", Reglas.NoVacio("product_name"));
            t.HasCheckConstraint("ck_special_order_leads_product_slug", Reglas.NoVacio("product_slug"));
            t.HasCheckConstraint("ck_special_order_leads_status", Reglas.EstadosDeSolicitud);
        });
        b.HasKey(x => x.Id).HasName("pk_special_order_leads");
        b.Property(x => x.Id).HasColumnName("special_order_lead_id").UseIdentityAlwaysColumn();
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.Property(x => x.ProductName).HasColumnName("product_name").IsRequired();
        b.Property(x => x.ProductSlug).HasColumnName("product_slug").IsRequired();
        b.Property(x => x.PendingRelink).HasColumnName("pending_relink").HasDefaultValue(false);
        b.Property(x => x.Description).HasColumnName("description").IsRequired();
        b.Property(x => x.Quantity).HasColumnName("quantity");
        b.Property(x => x.NeededBy).HasColumnName("needed_by");
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue(RequestStatus.Recibida);
        b.Property(x => x.StaffNotes).HasColumnName("staff_notes");
        Reglas.Tiempos(b);
        b.HasIndex(x => x.CustomerId).HasDatabaseName("idx_special_order_leads_customer");
        b.HasIndex(x => x.Status).HasDatabaseName("idx_special_order_leads_status");
        b.HasIndex(x => x.ProductId).HasDatabaseName("idx_special_order_leads_product");
    }
}

internal sealed class InstitutionRequestConfiguration : IEntityTypeConfiguration<InstitutionRequest>
{
    public void Configure(EntityTypeBuilder<InstitutionRequest> b)
    {
        b.ToTable("institution_requests", t =>
        {
            t.HasCheckConstraint("ck_institution_requests_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_institution_requests_description", Reglas.NoVacio("description"));
            t.HasCheckConstraint("ck_institution_requests_institution_name", Reglas.NoVacio("institution_name"));
            t.HasCheckConstraint("ck_institution_requests_status", Reglas.EstadosDeSolicitud);
        });
        b.HasKey(x => x.Id).HasName("pk_institution_requests");
        b.Property(x => x.Id).HasColumnName("institution_request_id").UseIdentityAlwaysColumn();
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.InstitutionName).HasColumnName("institution_name").IsRequired();
        b.Property(x => x.InstitutionDocument).HasColumnName("institution_document");
        b.Property(x => x.ContactPerson).HasColumnName("contact_person");
        b.Property(x => x.Description).HasColumnName("description").IsRequired();
        b.Property(x => x.Quantity).HasColumnName("quantity");
        b.Property(x => x.EventDate).HasColumnName("event_date");
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue(RequestStatus.Recibida);
        b.Property(x => x.StaffNotes).HasColumnName("staff_notes");
        Reglas.Tiempos(b);
        b.HasIndex(x => x.CustomerId).HasDatabaseName("idx_institution_requests_customer");
        b.HasIndex(x => x.Status).HasDatabaseName("idx_institution_requests_status");
    }
}

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> b)
    {
        b.ToTable("quotes", t =>
        {
            t.HasCheckConstraint("ck_quotes_origen", "(special_order_lead_id IS NULL) <> (institution_request_id IS NULL)");
            t.HasCheckConstraint("ck_quotes_total_amount", "total_amount >= 0");
            t.HasCheckConstraint("ck_quotes_number", Reglas.NoVacio("quote_number"));
            t.HasCheckConstraint("ck_quotes_status", "status IN ('borrador', 'enviada', 'aprobada', 'pagada', 'anulada')");
            t.HasCheckConstraint("ck_quotes_payment_method", "payment_method IS NULL OR payment_method IN ('yape', 'efectivo', 'tarjeta')");
        });
        b.HasKey(x => x.Id).HasName("pk_quotes");
        b.Property(x => x.Id).HasColumnName("quote_id").UseIdentityAlwaysColumn();
        b.Property(x => x.QuoteNumber).HasColumnName("quote_number").IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.SpecialOrderLeadId).HasColumnName("special_order_lead_id");
        b.Property(x => x.InstitutionRequestId).HasColumnName("institution_request_id");
        b.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(12,2)");
        b.Property(x => x.Status).HasColumnName("status").HasDefaultValue(QuoteStatus.Borrador);
        b.Property(x => x.InvalidatedAt).HasColumnName("invalidated_at").HasColumnType("timestamptz");
        b.Property(x => x.InvalidatedReason).HasColumnName("invalidated_reason");
        b.Property(x => x.ApprovedAt).HasColumnName("approved_at").HasColumnType("timestamptz");
        b.Property(x => x.PaidAt).HasColumnName("paid_at").HasColumnType("timestamptz");
        b.Property(x => x.PaymentMethod).HasColumnName("payment_method");
        b.Property(x => x.PaymentReference).HasColumnName("payment_reference");
        b.Property(x => x.PaidRegisteredBy).HasColumnName("paid_registered_by");
        Reglas.Tiempos(b);
        b.HasIndex(x => x.QuoteNumber).IsUnique().HasDatabaseName("uq_quotes_number");
        b.HasIndex(x => x.CustomerId).HasDatabaseName("idx_quotes_customer");
        b.HasIndex(x => x.Status).HasDatabaseName("idx_quotes_status");
        b.HasOne<SpecialOrderLead>().WithMany().HasForeignKey(x => x.SpecialOrderLeadId)
            .HasConstraintName("fk_quotes_special_order_lead").OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstitutionRequest>().WithMany().HasForeignKey(x => x.InstitutionRequestId)
            .HasConstraintName("fk_quotes_institution_request").OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.QuoteId)
            .HasConstraintName("fk_quote_lines_quote").OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuoteLineConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> b)
    {
        b.ToTable("quote_lines", t =>
        {
            t.HasCheckConstraint("ck_quote_lines_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_quote_lines_unit_price", "unit_price >= 0");
            t.HasCheckConstraint("ck_quote_lines_catalog_price", "catalog_price_at_quote IS NULL OR catalog_price_at_quote >= 0");
            t.HasCheckConstraint("ck_quote_lines_description", Reglas.NoVacio("description"));
            t.HasCheckConstraint("ck_quote_lines_sort_order", "sort_order >= 0");
            t.HasCheckConstraint("ck_quote_lines_snapshot",
                "(item_id IS NOT NULL OR (product_name IS NULL AND variant_value IS NULL AND sale_unit IS NULL AND catalog_price_at_quote IS NULL))"
                + " AND (item_id IS NULL OR (product_name IS NOT NULL AND btrim(product_name) <> ''))");
        });
        b.HasKey(x => x.Id).HasName("pk_quote_lines");
        b.Property(x => x.Id).HasColumnName("quote_line_id").UseIdentityAlwaysColumn();
        b.Property(x => x.QuoteId).HasColumnName("quote_id");
        b.Property(x => x.ItemId).HasColumnName("item_id");
        b.Property(x => x.ProductName).HasColumnName("product_name");
        b.Property(x => x.VariantValue).HasColumnName("variant_value");
        b.Property(x => x.SaleUnit).HasColumnName("sale_unit");
        b.Property(x => x.Description).HasColumnName("description").IsRequired();
        b.Property(x => x.Quantity).HasColumnName("quantity");
        b.Property(x => x.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)");
        b.Property(x => x.CatalogPriceAtQuote).HasColumnName("catalog_price_at_quote").HasColumnType("numeric(12,2)");
        b.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
        b.HasIndex(x => x.QuoteId).HasDatabaseName("idx_quote_lines_quote");
        b.HasIndex(x => x.ItemId).HasDatabaseName("idx_quote_lines_item");
    }
}

internal sealed class QuoteNumberSeriesConfiguration : IEntityTypeConfiguration<QuoteNumberSeries>
{
    public void Configure(EntityTypeBuilder<QuoteNumberSeries> b)
    {
        b.ToTable("quote_number_series", t =>
        {
            t.HasCheckConstraint("ck_quote_number_series_series_code", "series_code ~ '^[A-Z]$'");
            t.HasCheckConstraint("ck_quote_number_series_year", "year BETWEEN 2000 AND 9999");
            t.HasCheckConstraint("ck_quote_number_series_last_value", "last_value >= 0");
        });
        b.HasKey(x => new { x.SeriesCode, x.Year }).HasName("pk_quote_number_series");
        b.Property(x => x.SeriesCode).HasColumnName("series_code");
        b.Property(x => x.Year).HasColumnName("year");
        b.Property(x => x.LastValue).HasColumnName("last_value").HasDefaultValue(0);
    }
}

