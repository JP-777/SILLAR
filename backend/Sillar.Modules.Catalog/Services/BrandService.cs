using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Catalog.Domain;
using Sillar.Modules.Catalog.Dtos;

namespace Sillar.Modules.Catalog.Services;

/// <summary>Cómo terminó una operación sobre marcas.</summary>
internal enum BrandOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

/// <summary>Resultado de una operación sobre marcas.</summary>
internal sealed record BrandOperation(
    BrandOutcome Outcome,
    string? Error = null,
    BrandAdminResponse? Brand = null);

/// <summary>Marcas del catálogo: listado público y administración.</summary>
internal sealed class BrandService(
    CatalogDbContext database,
    IMediaStorage media,
    IAuditWriter audit)
{
    /// <summary>
    /// Marcas activas con al menos un producto público, para el filtro de la web.
    /// </summary>
    public async Task<IReadOnlyList<BrandResponse>> ListPublicAsync(CancellationToken cancellationToken)
    {
        // Se filtra y se ordena en la base, y se proyecta **en memoria**.
        // Resolver la URL de un medio es trabajo de cliente —`IMediaStorage`
        // no es traducible a SQL—, así que meterlo dentro del `Select` que
        // EF traduce hace que la consulta ni siquiera compile en tiempo de
        // ejecución. Ver el comentario de `ListAsync`.
        var brands = await database.Brands
            .AsNoTracking()
            .Where(brand => brand.IsActive && brand.Products.Any(product => product.IsActive && product.IsPublic))
            .OrderBy(brand => brand.Name)
            .ToListAsync(cancellationToken);

        return brands
            .Select(brand => new BrandResponse(brand.Slug, brand.Name, Logo(brand)))
            .ToList();
    }

    /// <summary>Todas las marcas, activas e inactivas, para la administración.</summary>
    /// <remarks>
    /// Se materializa **antes** de proyectar. Con `.Select(Project)` dentro de
    /// la consulta, EF Core aborta con <c>InvalidOperationException</c>: la
    /// proyección de cliente referencia a `BrandService` a través de un método
    /// de instancia, y eso capturaría el servicio dentro de la consulta
    /// compilada, que EF trata como fuga de memoria potencial.
    /// <para>
    /// Se descubrió al construir la pantalla de marcas: el endpoint devolvía
    /// 500 desde que se escribió. Las pruebas del módulo son de lógica pura y
    /// no tocan la base (`CLAUDE.md`), así que ninguna podía verlo — hace
    /// falta ejecutar la consulta de verdad.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<BrandAdminResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var brands = await database.Brands
            .AsNoTracking()
            .OrderBy(brand => brand.Name)
            .ToListAsync(cancellationToken);

        return brands.Select(Project).ToList();
    }

    /// <summary>Da de alta una marca.</summary>
    public async Task<BrandOperation> CreateAsync(
        CreateBrandRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Invalid("El nombre es obligatorio.");
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugGenerator.From(request.Name) : request.Slug.Trim();
        if (!SlugGenerator.IsValidFormat(slug))
        {
            return Invalid("El slug solo admite minúsculas, dígitos y guiones simples, sin uno al principio ni al final.");
        }

        var imageError = ValidateLogo(request.LogoId);
        if (imageError is not null)
        {
            return Invalid(imageError);
        }

        var brand = new Brand
        {
            Name = request.Name.Trim(),
            Slug = slug,
            LogoId = request.LogoId,
            IsActive = true
        };

        database.Brands.Add(brand);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return await DuplicatedAsync(exception, brand.Name, brand.Slug, cancellationToken);
        }

        await AuditAsync(AuditAction.Create, actingUserId, actingEmail, brand,
            $"Alta de la marca «{brand.Name}».", cancellationToken);

        return new BrandOperation(BrandOutcome.Ok, Brand: Project(brand));
    }

    /// <summary>Modifica una marca.</summary>
    public async Task<BrandOperation> UpdateAsync(
        Guid brandId,
        UpdateBrandRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Invalid("El nombre es obligatorio.");
        }

        if (!SlugGenerator.IsValidFormat(request.Slug))
        {
            return Invalid("El slug solo admite minúsculas, dígitos y guiones simples, sin uno al principio ni al final.");
        }

        var brand = await database.Brands.FirstOrDefaultAsync(b => b.Id == brandId, cancellationToken);
        if (brand is null)
        {
            return new BrandOperation(BrandOutcome.NotFound);
        }

        var imageError = ValidateLogo(request.LogoId);
        if (imageError is not null)
        {
            return Invalid(imageError);
        }

        brand.Name = request.Name.Trim();
        brand.Slug = request.Slug!.Trim();
        brand.LogoId = request.LogoId;
        brand.IsActive = request.IsActive;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return await DuplicatedAsync(exception, brand.Name, brand.Slug, cancellationToken);
        }

        await AuditAsync(AuditAction.Update, actingUserId, actingEmail, brand,
            $"Modificación de la marca «{brand.Name}».", cancellationToken);

        return new BrandOperation(BrandOutcome.Ok, Brand: Project(brand));
    }

    /// <summary>Desactiva una marca. Baja lógica: sus productos siguen existiendo.</summary>
    public async Task<BrandOperation> DeactivateAsync(
        Guid brandId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var brand = await database.Brands.FirstOrDefaultAsync(b => b.Id == brandId, cancellationToken);
        if (brand is null)
        {
            return new BrandOperation(BrandOutcome.NotFound);
        }

        if (!brand.IsActive)
        {
            return new BrandOperation(BrandOutcome.Ok, Brand: Project(brand));
        }

        brand.IsActive = false;
        await database.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditAction.Delete, actingUserId, actingEmail, brand,
            $"Baja de la marca «{brand.Name}».", cancellationToken);

        return new BrandOperation(BrandOutcome.Ok, Brand: Project(brand));
    }

    private string? ValidateLogo(Guid? logoId)
        => logoId is null || media.GetPublicUrl(logoId.Value) is not null
            ? null
            : "El logotipo indicado no existe o no está activo.";

    private Task AuditAsync(
        string action,
        int actingUserId,
        string actingEmail,
        Brand affected,
        string summary,
        CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CatalogModule.ModuleCode,
                EntityType = "brand",
                EntityId = affected.Id.ToString(),
                Summary = summary
            },
            cancellationToken);

    private static BrandOperation Invalid(string error) => new(BrandOutcome.Invalid, error);

    /// <summary>
    /// Redacta el conflicto según <b>qué</b> restricción chocó, no en general.
    /// </summary>
    /// <remarks>
    /// Un mensaje único —«ya existe con ese nombre o ese slug»— obliga a quien lo
    /// lee a adivinar cuál de los dos campos corregir, y encima le explica lo de
    /// las mayúsculas a quien chocó por la dirección web, que es otro problema.
    /// <para>
    /// El caso del nombre necesita explicarse: la unicidad usa <c>core.es_ci</c>,
    /// que ignora mayúsculas y respeta tildes (regla 13 del SPEC). Quien escribe
    /// «ARTESCO» teniendo «Artesco» no ve por qué choca si no se le dice.
    /// </para>
    /// Estos mensajes son texto de interfaz: la pantalla los muestra tal cual.
    /// </remarks>
    private async Task<BrandOperation> DuplicatedAsync(
        DbUpdateException exception,
        string name,
        string slug,
        CancellationToken cancellationToken)
    {
        var constraint = (exception.InnerException as PostgresException)?.ConstraintName;

        if (constraint == "uq_brands_slug")
        {
            return new BrandOperation(
                BrandOutcome.Conflict,
                $"Otra marca ya usa la dirección web «{slug}». Elige otra en el campo de dirección.");
        }

        // Se busca **la que ya existe** para poder enseñar su grafía. Repetir
        // la que acaba de teclear no le dice nada a nadie: en pantalla no hay
        // dos nombres iguales, y sin ver el otro no se entiende qué choca.
        // La comparación la hace la colación es_ci de la columna, así que una
        // igualdad simple encuentra «Faber-Castell» buscando «FABER-CASTELL».
        var existing = await database.Brands
            .AsNoTracking()
            .Where(brand => brand.Name == name)
            .Select(brand => brand.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var message = existing is null || existing == name
            ? $"Ya existe una marca llamada «{name}»."
            : $"Ya existe una marca llamada «{existing}». Los nombres no distinguen mayúsculas, " +
              $"así que «{name}» y «{existing}» son la misma marca.";

        return new BrandOperation(BrandOutcome.Conflict, message);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>URL del logotipo, o <c>null</c> si no tiene o ya no está.</summary>
    private string? Logo(Brand brand) => brand.LogoId is { } logoId ? media.GetPublicUrl(logoId) : null;

    private BrandAdminResponse Project(Brand brand) => new(
        brand.Id,
        brand.Name,
        brand.Slug,
        brand.LogoId,
        Logo(brand),
        brand.IsActive);
}
