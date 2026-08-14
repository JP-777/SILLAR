using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Core.Data;

namespace Sillar.Core.Settings;

/// <summary>
/// Implementación de <see cref="ISettingsReader"/> con caché en memoria.
/// </summary>
/// <remarks>
/// La configuración se lee en cada petición pública y cambia unas pocas veces al
/// año, así que se cachea entera: son unas decenas de filas.
///
/// Es un singleton y abre su propio ámbito para leer, porque el
/// <c>DbContext</c> tiene vida de petición y aquí haría falta más tiempo. La
/// escritura invalida, y la siguiente lectura recarga.
/// </remarks>
internal sealed class SettingsCache(IServiceScopeFactory scopeFactory) : ISettingsReader
{
    private readonly Lock _gate = new();
    private Dictionary<string, Entry>? _entries;

    /// <inheritdoc />
    public string? Get(string key)
        => Entries().TryGetValue(key, out var entry) ? entry.Value : null;

    /// <inheritdoc />
    public T? Get<T>(string key)
    {
        var raw = Get(key);
        if (raw is null)
        {
            return default;
        }

        try
        {
            // Cultura invariante: el valor se guarda como texto y no puede
            // depender de la configuración regional de la máquina.
            return (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            // Convertir mal no es motivo para tumbar una petición: quien pide un
            // número y encuentra texto recibe el valor por defecto y sigue.
            return default;
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetPublic()
        => Entries()
            .Where(entry => entry.Value.IsPublic)
            .ToDictionary(entry => entry.Key, entry => entry.Value.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>Descarta la caché. La siguiente lectura vuelve a la base de datos.</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _entries = null;
        }
    }

    private Dictionary<string, Entry> Entries()
    {
        // Doble comprobación: la lectura es lo habitual y no debe tomar el
        // cerrojo una vez cargada la caché.
        var loaded = Volatile.Read(ref _entries);
        if (loaded is not null)
        {
            return loaded;
        }

        lock (_gate)
        {
            return _entries ??= Load();
        }
    }

    private Dictionary<string, Entry> Load()
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        // Solo las activas: desactivar una clave es la forma de retirarla, así
        // que dejar de servirla es justo el efecto buscado.
        return database.SiteSettings
            .AsNoTracking()
            .Where(setting => setting.IsActive)
            .Select(setting => new { setting.SettingKey, setting.SettingValue, setting.IsPublic })
            .ToDictionary(
                setting => setting.SettingKey,
                setting => new Entry(setting.SettingValue, setting.IsPublic),
                StringComparer.OrdinalIgnoreCase);
    }

    private readonly record struct Entry(string Value, bool IsPublic);
}
