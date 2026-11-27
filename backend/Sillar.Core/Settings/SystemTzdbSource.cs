using System.Security.Cryptography;
using System.Text;

namespace Sillar.Core.Settings;

/// <summary>Fuente IANA del runtime Linux de WEB. Su ausencia nunca se convierte en una acreditación.</summary>
internal sealed class SystemTzdbSource : ITzdbSource
{
    internal const string SourceName = "system-tzdata.zi";
    private const string SourcePath = "/usr/share/zoneinfo/tzdata.zi";

    public TzdbObservation Observe(string zoneId)
    {
        try
        {
            var text = File.ReadAllText(SourcePath);
            // La configuración puede cambiar de reglas después de actualizar tzdata.
            // El resolver posterior debe usar las reglas actuales, no la caché vieja.
            TimeZoneInfo.ClearCachedData();
            _ = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            return ObserveText(zoneId, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return TzdbObservation.Unavailable;
        }
    }

    internal static TzdbObservation ObserveText(string zoneId, string text)
    {
        var lines = text.Split('\n');
        var version = lines.FirstOrDefault(x => x.StartsWith("# version ", StringComparison.Ordinal))?[10..].Trim();
        if (string.IsNullOrWhiteSpace(version) || version.Length > 30) return TzdbObservation.Unavailable;
        var canonical = lines.Any(line =>
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 2 && (parts[0] is "Z" or "Zone") && parts[1] == zoneId;
        });
        // La versión declarada sola no detectaría un archivo cambiado conservando su cabecera.
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return new(true, SourceName, $"{version}:{fingerprint}", canonical);
    }
}
