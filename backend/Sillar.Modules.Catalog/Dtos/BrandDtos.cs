namespace Sillar.Modules.Catalog.Dtos;

/// <summary>Una marca, para la web pública.</summary>
/// <param name="Slug">Para la URL pública / el filtro.</param>
/// <param name="Name">Nombre visible.</param>
/// <param name="LogoUrl">Logotipo, si tiene.</param>
public sealed record BrandResponse(string Slug, string Name, string? LogoUrl);

/// <summary>Una marca, vista desde la administración.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Name">Nombre visible.</param>
/// <param name="Slug">Para la URL pública.</param>
/// <param name="LogoId">Logotipo, en <c>core.media_assets</c>.</param>
/// <param name="LogoUrl">Logotipo, ya resuelto.</param>
/// <param name="IsActive">Baja lógica.</param>
public sealed record BrandAdminResponse(
    Guid Id,
    string Name,
    string Slug,
    Guid? LogoId,
    string? LogoUrl,
    bool IsActive);

/// <summary>Da de alta una marca.</summary>
/// <param name="Name">Obligatorio. Único ignorando mayúsculas, respetando tildes.</param>
/// <param name="Slug">Opcional: si falta, se genera del nombre.</param>
/// <param name="LogoId">Opcional. Debe existir en <c>core.media_assets</c> y estar activo.</param>
public sealed record CreateBrandRequest(string? Name, string? Slug, Guid? LogoId);

/// <summary>Modifica una marca.</summary>
/// <param name="Name">Obligatorio.</param>
/// <param name="Slug">Obligatorio, tal cual se envía (regla 3: no se recalcula).</param>
/// <param name="LogoId">Opcional.</param>
/// <param name="IsActive">Baja lógica.</param>
public sealed record UpdateBrandRequest(string? Name, string? Slug, Guid? LogoId, bool IsActive);
