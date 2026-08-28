using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Core.Contracts.Events;
using Sillar.Core.Contracts.Email;
using Sillar.Core.Data;
using Sillar.Core.Dtos;
using Sillar.Core.Settings;
using Sillar.Shared.Events;

namespace Sillar.Core.Services;

/// <summary>Cómo terminó un cambio de configuración.</summary>
internal enum SettingOutcome
{
    /// <summary>Guardado.</summary>
    Ok,

    /// <summary>Esa clave no existe. No se crea.</summary>
    NotFound,

    /// <summary>El valor no encaja con el tipo declarado.</summary>
    Invalid,

    /// <summary>Hace falta <c>super_admin</c> para lo que se pide.</summary>
    Forbidden
}

/// <summary>Resultado de un cambio de configuración.</summary>
internal sealed record SettingOperation(
    SettingOutcome Outcome,
    string? Error = null,
    SettingResponse? Setting = null);

/// <summary>Configuración general del sitio.</summary>
internal sealed class SiteSettingService(
    CoreDbContext database,
    SettingsCache cache,
    IAuditWriter audit,
    IEventPublisher events,
    TimeProvider clock)
{
    /// <summary>
    /// Marcador que deja el seed en las claves que el negocio debe completar.
    /// </summary>
    public const string PendingMarker = "PENDIENTE_DEFINIR";

    /// <summary>Lista todas las configuraciones, activas e inactivas.</summary>
    public async Task<IReadOnlyList<SettingResponse>> ListAsync(CancellationToken cancellationToken)
        => await database.SiteSettings
            .AsNoTracking()
            .OrderBy(setting => setting.SettingKey)
            .Select(setting => new SettingResponse(
                setting.SettingKey,
                setting.SettingValue,
                setting.ValueType,
                setting.Description,
                setting.IsPublic,
                setting.IsActive,
                setting.SettingValue == PendingMarker,
                setting.UpdatedAt))
            .ToListAsync(cancellationToken);

    /// <summary>Cambia el valor de una clave, y opcionalmente su visibilidad.</summary>
    /// <param name="key">Clave a modificar. Debe existir.</param>
    /// <param name="request">Valor nuevo y, si se quiere cambiar, la visibilidad.</param>
    /// <param name="canChangeVisibility">Si quien pide es <c>super_admin</c>.</param>
    /// <param name="actingUserId">Quién lo hace.</param>
    /// <param name="actingEmail">Correo de quien lo hace.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    public async Task<SettingOperation> UpdateAsync(
        string key,
        UpdateSettingRequest request,
        bool canChangeVisibility,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        // La colación core.es_ci hace que esta igualdad no distinga mayúsculas.
        var setting = await database.SiteSettings
            .FirstOrDefaultAsync(candidate => candidate.SettingKey == key, cancellationToken);

        // Las claves nacen del seed o de la migración del módulo que las
        // necesita. Si el API pudiera crearlas, site_settings acabaría siendo un
        // cajón de sastre sin tipo ni descripción.
        if (setting is null)
        {
            return new SettingOperation(SettingOutcome.NotFound);
        }

        var wantsVisibilityChange = request.IsPublic is { } requested && requested != setting.IsPublic;
        var isMailSetting = EmailSettingsKeys.IsMailSetting(setting.SettingKey);

        if (isMailSetting && !canChangeVisibility)
        {
            return new SettingOperation(
                SettingOutcome.Forbidden,
                "La configuración de correo exige el rol super_admin.");
        }

        if (wantsVisibilityChange && !canChangeVisibility)
        {
            return new SettingOperation(
                SettingOutcome.Forbidden,
                "Cambiar si una configuración es pública exige el rol super_admin.");
        }

        var invalid = SettingValueValidator.Validate(setting.ValueType, request.Value);
        if (invalid is not null)
        {
            return new SettingOperation(
                SettingOutcome.Invalid,
                $"El valor no encaja con el tipo '{setting.ValueType}'. {invalid}");
        }

        var previousValue = setting.SettingValue;
        setting.SettingValue = request.Value!.Trim();

        if (wantsVisibilityChange)
        {
            setting.IsPublic = request.IsPublic!.Value;
        }

        await database.SaveChangesAsync(cancellationToken);

        // La caché se descarta después de guardar: si se descartara antes, una
        // lectura simultánea recargaría el valor viejo y lo dejaría fijado.
        cache.Invalidate();

        await audit.WriteAsync(
            new AuditEntry(AuditAction.Update)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "setting",
                EntityId = setting.SettingKey,
                // Sin el valor, ni el nuevo ni el anterior. Hoy todas las claves
                // del seed son inocuas, pero esta tabla está pensada para alojar
                // también credenciales de correo saliente, y la auditoría no se
                // puede borrar (SPEC §8.15): registrarlos la convertiría en un
                // almacén permanente de secretos en claro.
                Summary = isMailSetting
                    ? $"Configuración de correo '{setting.SettingKey}' actualizada: '{previousValue}' -> '{setting.SettingValue}'."
                    : wantsVisibilityChange
                        ? $"Configuración '{setting.SettingKey}' actualizada. Visibilidad pública: {setting.IsPublic}."
                        : $"Configuración '{setting.SettingKey}' actualizada."
            },
            cancellationToken);

        await events.PublishAsync(
            new SettingChanged(setting.SettingKey, setting.IsPublic, clock.GetUtcNow()),
            cancellationToken);

        return new SettingOperation(
            SettingOutcome.Ok,
            Setting: new SettingResponse(
                setting.SettingKey,
                setting.SettingValue,
                setting.ValueType,
                setting.Description,
                setting.IsPublic,
                setting.IsActive,
                setting.SettingValue == PendingMarker,
                setting.UpdatedAt));
    }
}
