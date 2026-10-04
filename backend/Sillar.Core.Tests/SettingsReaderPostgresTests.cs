using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Core.Domain;
using Sillar.Core.Domain.Values;
using Sillar.Core.Settings;
using Sillar.Shared.Data.Modularity;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// Frontera PostgreSQL del camino frío de ISettingsReader.Get&lt;T&gt;.
/// </summary>
public sealed class SettingsReaderPostgresTests
{
    [Fact]
    public async Task GetT_con_cache_fria_lee_PostgreSQL_y_respeta_conversion_defaults_e_inactivos()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            var core = Instalador().Orden().Single(module => module.Code == "core");
            await ((IModuleMigrations)core).ApplyMigrationsAsync(cadena, ct);

            await using (var preparar = Contexto(cadena))
            {
                preparar.SiteSettings.AddRange(
                    new SiteSetting
                    {
                        SettingKey = "qa_gett_numero_valido",
                        SettingValue = "42",
                        ValueType = SettingValueType.Number,
                        IsPublic = false,
                        IsActive = true
                    },
                    new SiteSetting
                    {
                        SettingKey = "qa_gett_no_convertible",
                        SettingValue = "no-es-un-entero",
                        ValueType = SettingValueType.Text,
                        IsPublic = false,
                        IsActive = true
                    },
                    new SiteSetting
                    {
                        SettingKey = "qa_gett_inactiva",
                        SettingValue = "99",
                        ValueType = SettingValueType.Number,
                        IsPublic = false,
                        IsActive = false
                    });

                await preparar.SaveChangesAsync(ct);
            }

            var services = new ServiceCollection();

            // Mismo contrato e implementación de producción para esta frontera.
            // El CoreDbContext scoped apunta a la base PostgreSQL efímera real.
            services.AddScoped(_ => Contexto(cadena));
            services.AddSingleton<SettingsCache>();
            services.AddSingleton<ISettingsReader>(
                provider => provider.GetRequiredService<SettingsCache>());

            await using var provider = services.BuildServiceProvider();

            // SettingsCache todavía no ha ejecutado Entries()/Load():
            // esta es la primera lectura de una instancia nueva.
            ISettingsReader reader = provider.GetRequiredService<ISettingsReader>();

            Assert.Equal(42, reader.Get<int>("qa_gett_numero_valido"));
            Assert.Equal(default, reader.Get<int>("qa_gett_inexistente"));
            Assert.Equal(default, reader.Get<int>("qa_gett_no_convertible"));
            Assert.Equal(default, reader.Get<int>("qa_gett_inactiva"));

            // Demuestra la separación entre el primer Load() desde PostgreSQL
            // y las lecturas posteriores desde la caché de la misma instancia.
            await using (var modificar = Contexto(cadena))
            {
                var fila = await modificar.SiteSettings.SingleAsync(
                    setting => setting.SettingKey == "qa_gett_numero_valido",
                    ct);

                fila.SettingValue = "84";
                await modificar.SaveChangesAsync(ct);
            }

            Assert.Equal(42, reader.Get<int>("qa_gett_numero_valido"));
        }, ct);
    }
}
